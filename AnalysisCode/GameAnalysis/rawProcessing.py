import numpy as np
import pandas as pd
import os
# import datetime as dt
# import math


def find_nearest_iso(tickTimes, tapTimes):
    # For non-isochronous stimuli, just return idx and mask, and those will tell you which element is the closest and
    # whether the second closest is before or after
    tickTimes = np.asarray(tickTimes)
    tapTimes = np.asarray(tapTimes)
    idx = np.searchsorted(tickTimes, tapTimes, side="left").clip(max=tickTimes.size - 1)
    mask = (idx > 0) & \
           ((idx == len(tickTimes)) | (np.fabs(tapTimes - tickTimes[idx - 1]) < np.fabs(tapTimes - tickTimes[idx])))
    return tickTimes[idx - mask]


def get_angles(tickTimes, tapTimes):
    # For non-isochronous stimuli, just return idx and mask, and those will tell you which element is the closest and
    # whether the second closest is before or after
    tickTimes = np.asarray(tickTimes)
    tapTimes = np.asarray(tapTimes)

    # np.searchsorted effectively returns the index of the next tick, so idx-1 is the tick before
    idx = np.searchsorted(tickTimes, tapTimes, side="left").clip(max=tickTimes.size - 1)

    # If tap is closer to previous tick then shift index by -1
    mask = (idx > 0) & \
           ((idx == len(tickTimes)) | (np.fabs(tapTimes - tickTimes[idx - 1]) < np.fabs(tapTimes - tickTimes[idx])))
    nearestTicks = tickTimes[idx - mask]

    IOIlist = np.diff(tickTimes)
    # assume interval before first tick is the same as the first actual interval
    IOIlist = np.insert(IOIlist, 0, IOIlist[0])
    phaseAngles = 2 * np.pi * (tapTimes - nearestTicks) / IOIlist[idx]

    # are there any taps after the last tick that would technically be closer to the following tick?
    anglesAfter = np.abs(phaseAngles[-1]) > np.pi
    if anglesAfter:
        # add a phantom tick with the same interval as the last one
        tickTimes = np.append(tickTimes, tickTimes[-1]+IOIlist[-1])
        # and rerun the analysis
        nearestTicks, phaseAngles = get_angles(tickTimes, tapTimes)
    return nearestTicks, phaseAngles


def beat_time_from_beatzone(beatZoneOnset, wheelSpeed, beatZoneSize):
    # If the last beatZone time is after the last tick time, we can assume the trial ended before the final
    # tick was recorded

    # Note this is not perfectly accurate because the physics engine updates at a fixed rate
    # so event timing is rounded
    # We don't know width of beatMarker because it's scaled to screen, 0.01 is approx.
    # might be easier to figure out the actual beatZone to beat offsets and just use the
    # average of those
    beatTime = beatZoneOnset + ((beatZoneSize - 0.01) / (2 * wheelSpeed))
    return beatTime  # tempTickTimes.loc[tempTickTimes.index.max() + 1] = nextBeatTime


def version_atleast(currVersion, targetVersion):
    # quick function to check if currVersion is targetVersion or later
    currVerArray = currVersion.split(".")
    targetVerArray = targetVersion.split(".")
    currVerArray = [int(x) for x in currVerArray]
    targetVerArray = [int(x) for x in targetVerArray]

    for y in range(len(currVerArray)):
        if currVerArray[y] < targetVerArray[y]:
            return False
    return True


# analyze all
# get all session data files
rawFolder = 'D:\\Rouse\\My Documents\\GitHub\\RhythmGame\\AnalysisCode\\GameAnalysis\\Input'
outputFolder = 'D:\\Rouse\\My Documents\\GitHub\\RhythmGame\\AnalysisCode\\GameAnalysis\\Output'

allData = pd.DataFrame()
allTaps = pd.DataFrame()

print("Analyzing...")

# get list of folders in data folder, which is list of subjects
# https://stackoverflow.com/questions/7781545/how-to-get-all-folder-only-in-a-given-path-in-python
subjectList = next(os.walk(rawFolder))[1]
for subject in subjectList:
    print("  Subject: {}".format(subject))
    subjectFolder = os.path.join(rawFolder, subject)

    # get subject info
    sessionList = list(filter(lambda s: s.endswith('.txt'), next(os.walk(subjectFolder))[2]))
    # reorder files by date so session numbering is accurate
    dfSession = pd.DataFrame({'Filename': sessionList})
    sessionTS = dfSession['Filename'].str.slice(start=-18, stop=-4)
    dfSession['TS'] = pd.to_datetime(sessionTS)
    dfSession = dfSession.sort_values(by='TS')

    sessionNumber = 0

    for fileIndex, fileRow in dfSession.iterrows():
        sessionNumber += 1
        currFile = fileRow['Filename']
        print("    File: {}".format(currFile))

        currPath = os.path.join(subjectFolder, currFile)
        currData = pd.read_table(currPath,
                                 header=None,
                                 names=['Time', 'Type', 'Message', 'Value'],
                                 dtype={
                                     'Time': float,
                                     'Type': str,
                                     'Message': str,
                                     'Value': str

                                 }  # manually assign dtypes to each column
                                 )
        currDate = fileRow['TS'].date()
        # currData['Timestamp'] = pd.to_datetime(currData['Timestamp'])
        currData.insert(0, column='File', value=currFile)
        currData.insert(1, column='Subject', value=subject)
        currData.insert(3, column='Date', value=currDate)
        # currData['Date'] = currData['Date'].dt.date
        currData['SessionNum'] = sessionNumber
        # get phase number
        currPhase = currData[currData['Type'].str.match('Session') &
                             currData['Message'].str.match('Phase')]['Value'].min()
        currPhase = int(currPhase)
        currData['PhaseNum'] = currPhase

        # get current version (some things change between versions)
        gameVersion = currData[currData['Type'].str.match('Game') & currData['Message'].str.match('Version')]['Value']
        if len(gameVersion) == 0:
            # default to version number of 2.5.2 - version numbering was introduced in 2.5.3
            gameVersion = '2.5.2'
        else:
            gameVersion = gameVersion[0]
        # # add trial number
        # vectorized function, faster than iterrows()
        currData['Trial'] = currData['Type'].str.contains('Trial') & currData['Message'].str.contains('started')
        currData['Trial'] = currData['Trial'].cumsum() - 1
        # replace trial number for Trial Data events if game version is below 2.5.3
        if not version_atleast(gameVersion, '2.5.3'):
            # prior to 2.5.3, trial params were recorded before the trial start message
            currData['Trial'] = np.where(currData['Type'].str.contains('Trial Param'), currData['Trial']+1,
                                         currData['Trial'])
        # # calculate event times based on trial start
        trialStartTimes = currData.groupby(['Trial'])['Time'].min()
        currData['TrialStart'] = currData['Trial'].map(trialStartTimes)
        currData['TrialTime'] = (currData['Time'] - currData['TrialStart'])
        currData['RawTrialTime'] = currData['TrialTime']  # copy of trial time because TrialTime will be modified by LRS

        # # remove LRS times - create column for running total of LRS time, subtract from trial time
        # mark events for LRS start and LRS end with 0 and 1
        currData['LRS_start'] = currData['Type'].str.contains('Feedback') & currData['Message'].str.contains(
            'initiated')
        currData['LRS_end'] = currData['Type'].str.contains('Feedback') & currData['Message'].str.contains(
            'ended')
        # cumsum both, get min time for each group in each column
        currData['LRS_start_group'] = currData.groupby('Trial')['LRS_start'].cumsum()
        currData['LRS_end_group'] = currData.groupby('Trial')['LRS_end'].cumsum()
        # get min time of LRS_start_group and LRS_end_group for each group in both columns
        LRSstart = currData.groupby(['Trial', 'LRS_start_group'])['TrialTime'].min().reset_index()
        currData = currData.merge(LRSstart, on=['Trial', 'LRS_start_group'], how="left", suffixes=("", "_LRS_start"))
        LRSend = currData.groupby(['Trial', 'LRS_end_group'])['TrialTime'].min().reset_index()
        currData = currData.merge(LRSend, on=['Trial', 'LRS_end_group'], how="left", suffixes=("", "_LRS_end"))
        # LRS_diff = group_n_end - group_n_start
        currData["LRS_diff"] = currData['LRS_end']*(currData['TrialTime_LRS_end'] - currData['TrialTime_LRS_start'])
        # LRSoffset = LRS_diff where LRS ends
        currData["LRSoffset"] = currData.groupby('Trial')["LRS_diff"].cumsum()
        # drop temp columns
        currData.drop(columns=["LRS_start", "LRS_end", "LRS_start_group", "LRS_end_group",
                               "TrialTime_LRS_end", "TrialTime_LRS_start", "LRS_diff"],
                      inplace=True)
        currData['TrialTime'] = currData['TrialTime'] - currData['LRSoffset']

        currTapTimes = currData[currData['Type'].str.match('Response')].copy()

        # calculate angles for each tap
        trialCount = currData['Trial'].max() + 1
        angleColumn = []
        closeTickColumn = []
        if currPhase != 0:
            # phase 0 has no tempo so no phases can be calculated
            for i in range(trialCount):
                # break into trials
                print("      Trial %i" % i)
                currTrial = currData[currData['Trial'] == i]
                tempTapTimes = currTrial[currTrial['Type'].str.match('Response')]['TrialTime']
                if len(tempTapTimes) > 0:

                    # add next tick time to tick times if final tap occurred before tick onset (beatZone entered but
                    # no tick)

                    # To be a criterion tap, tap has to fall within the beatZone, which means beatZone start would
                    # be logged. If we have that, beatZone width, and the wheel tempo we can calculate where the
                    # beat would have been

                    tempTickTimes = currTrial[currTrial['Message'].str.match('Beat tick')]['TrialTime']
                    # .reset_index() on separate line so IDE doesn't complain about line being too long
                    tempTickTimes = tempTickTimes.reset_index(drop=True)
                    tempBeatZoneTimes = currTrial[currTrial['Message'].str.match('Beat zone start')]['TrialTime']
                    tempBeatZoneTimes = tempBeatZoneTimes.reset_index(drop=True)
                    if len(tempBeatZoneTimes) == 0:
                        # fringe case: trial cancelled before any beats were registered but subject still tapped
                        if len(tempTapTimes) > 1:
                            # multiple taps need an empty array for angles
                            angles = np.array([np.nan] * len(tempTapTimes))
                            # angles[:] = np.nan
                            closeTicks = np.array([np.nan] * len(tempTapTimes))
                        else:
                            # but a single tap only needs one value
                            angles = np.nan
                            closeTicks = np.nan
                    else:
                        # at least one beatZone occurred so we can calculate onset of beat, if needed
                        lastBZtime = tempBeatZoneTimes.iloc[-1]
                        tempTrialParam = currTrial[currTrial['Type'].str.match('Trial Param')]
                        tickPattern = tempTrialParam[tempTrialParam['Message'].str.match('Event List')]['Value']
                        tickPattern = [float(x) for x in tickPattern.item().split(", ")]
                        tempWheelSpeed = tempTrialParam[tempTrialParam['Message'].str.match('Wheel Tempo')]['Value']
                        tempWheelSpeed = float(tempWheelSpeed.item())
                        tempWheelSpeed = 360 * tempWheelSpeed  # convert revolutions/sec to degrees/sec
                        tempBZsize = tempTrialParam[tempTrialParam['Message'].str.match('Beat Zone Size')]['Value']
                        tempBZsize = float(tempBZsize.item())  # bzSize is in degrees

                        if len(tempTickTimes) == 0:
                            # rare case where criteria is 1 and the tap came before the first tick actually happened
                            # add that tick's theoretical time

                            nextBeatTime = beat_time_from_beatzone(lastBZtime, tempWheelSpeed, tempBZsize)
                            tempTickTimes = pd.concat([tempTickTimes, pd.Series([nextBeatTime])],
                                                      ignore_index=True)  # df.append was removed as of Pandas 2.0

                        # now tempTickTimes should always have at least 1 value
                        lastTickTime = tempTickTimes.iloc[-1]

                        if lastBZtime > lastTickTime:
                            # if the last beatZone time is after the last tick time, we can assume the trial ended
                            # before the final tick was recorded
                            nextBeatTime = beat_time_from_beatzone(lastBZtime, tempWheelSpeed, tempBZsize)
                            tempTickTimes = pd.concat([tempTickTimes, pd.Series([nextBeatTime])],
                                                      ignore_index=True)
                        else:
                            # Last tap was closer to the most recent tick, so we still need to calculate next tick
                            # onset to get the current interval
                            # calculate if pattern is isochronous or not

                            isochronous = len(set(tickPattern)) == 1

                            if isochronous:
                                intervalSize = 360/sum(tickPattern)
                            else:
                                # much more complicated because we have to determine which interval we're on
                                nTicks = len(tempTickTimes)
                                tickPatternSize = len(tickPattern)

                                # Do we have all the ticks? Because we're in the section where lastTick happened after
                                # last beatZone, there should be a tick event for every actual tick
                                if nTicks == len(tempBeatZoneTimes):
                                    currInterval = (nTicks-1) % tickPatternSize

                                else:
                                    # We're missing one or more ticks so we need to compare the actual times to the
                                    # theoretical ticks and see if any gaps are too large
                                    print("        Warning, possible ticks missing: {} ticks vs {} beat zones".format(
                                        len(tempTickTimes), len(tempBeatZoneTimes)))
                                    # # MORE ACCURATE METHOD BELOW
                                    # # generate list of theoretical tick times, longer than the actual number of ticks
                                    # tickListTheor = np.asarray(tickPattern * (int(nTicks/tickPatternSize)+2))
                                    # # convert tickListTheor to onset times
                                    # tickListTheor = tickListTheor/sum(tickPattern)/tempWheelSpeed
                                    # tickListTheor = np.insert(tickListTheor, 0, 0)
                                    # tickListTheor = tickListTheor + tempTickTimes[0]

                                    # ** more accurate that minimizes possible drift: add each successive interval to
                                    # the actual tick time to see if it approximately equals the next tick

                                    # loop through actual and theoretical ticks and look for mismatches
                                    tickTimesCorr = []
                                    tickIndex = 0
                                    nextExp = tempTickTimes[0]
                                    tickPatternTime = np.asarray(tickPattern) * 360 / sum(tickPattern) / tempWheelSpeed
                                    patternIndex = 0
                                    tolerance = 0.05
                                    while tickIndex < nTicks:
                                        nextObs = tempTickTimes[tickIndex]

                                        if abs(nextObs - nextExp) <= tolerance:
                                            tickTimesCorr.append(nextObs)
                                            tickIndex += 1

                                        elif nextObs > nextExp:
                                            tickTimesCorr.append(nextExp)
                                            # patternIndex -= 1
                                        else:
                                            tickIndex += 1
                                        nextExp = tickTimesCorr[-1] + tickPatternTime[patternIndex % tickPatternSize]
                                        patternIndex += 1

                                    currInterval = patternIndex % tickPatternSize
                                    tempTickTimes = tickTimesCorr
                                intervalSize = tickPattern[currInterval] * 360 / sum(tickPattern)

                            tempInterval = intervalSize / tempWheelSpeed
                            nextBeatTime = lastTickTime + tempInterval
                            tempTickTimes = pd.concat([tempTickTimes, pd.Series([nextBeatTime])],
                                                      ignore_index=True)

                        if len(tempTickTimes) == 1:
                            # where we needed to add the first (and only) tick, we need to add a second one so
                            # get_angles() can have proper interval data to calculate angle
                            intervalSize = tickPattern[0] * 360 / sum(tickPattern)
                            tempInterval = intervalSize / tempWheelSpeed
                            nextBeatTime = lastTickTime + tempInterval
                            tempTickTimes = pd.concat([tempTickTimes, pd.Series([nextBeatTime])],
                                                      ignore_index=True)

                        # # Now that we have ticks and taps, get tap angles
                        # get_angles() assumes that interval before the first beat is the same as the first interval
                        [closeTicks, angles] = get_angles(tempTickTimes, tempTapTimes)

                    # np.ravel is required because sometimes closeTicks is ndarray and sometimes a single float
                    # extend() only works on iterables, and append() adds as a single item
                    closeTickColumn.extend(np.ravel(closeTicks))
                    angleColumn.extend(np.ravel(angles))

            currTapTimes['Closest Ticks'] = closeTickColumn
            currTapTimes['Angle'] = angleColumn
            currTapTimes.drop(columns=["Type", "Value"], inplace=True)
            # what session-level data to store? Or keep session-level data in separate file for lookup?
            # session level data to get/calc: session number, phase number
            # get session-level data

            currSessionData = currData[currData['Type'].str.match('Trial Param')]

            # # add current data to total dataset
            allTaps = pd.concat([allTaps, currTapTimes], ignore_index=True)
            allData = pd.concat([allData, currData], ignore_index=True)


# export phases in table format with all data
allData.to_csv(os.path.join(outputFolder, 'allData.csv'))
allTaps.to_csv(os.path.join(outputFolder, 'allTapData.csv'))
print("Finished!")
