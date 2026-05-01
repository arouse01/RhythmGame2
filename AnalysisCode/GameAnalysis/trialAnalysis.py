import numpy as np
import pandas as pd
import os
# import datetime as dt
# import math


def circ_mean(angles, r=None):
    # r is optional variable for averaging mean angles - list of equal length of angles with the radius for each mean
    # angle. Default is 1 (unweighted)
    if r is None:
        r = np.ones_like(angles)
    mean_sine = average_sin(angles, r)
    mean_cosine = average_cos(angles, r)
    circular_mean_radians = np.arctan2(mean_sine, mean_cosine)
    return circular_mean_radians


def average_sin(angles, r=None):
    # from CH
    # 4/18/25 (AR) Added the r parameter for averaging means
    # specifically so we can put sin in the output file
    if r is None:
        r = np.ones_like(angles)
    angles = pd.to_numeric(angles, errors='coerce')
    return np.mean(np.sin(angles) * r)


def average_cos(angles, r=None):
    # from CH
    # 4/18/25 (AR) Added the r parameter for averaging means
    # specifically so we can put cos in the output file
    if r is None:
        r = np.ones_like(angles)
    angles = pd.to_numeric(angles, errors='coerce')
    return np.mean(np.cos(angles) * r)


def circ_r(angles, r=None):
    # r is optional variable for averaging lengths - list of equal length of angles with the radius for each mean
    # angle. Default is 1 (unweighted)
    if r is None:
        r = np.ones_like(angles)
    mean_sine = average_sin(angles, r)
    mean_cosine = average_cos(angles, r)
    vector_length = np.sqrt(mean_sine**2 + mean_cosine**2)
    return vector_length


def safe_count(messages):
    return ((messages == 'Safe') | (messages == 'Hit')).sum()


def miss_count(messages):
    return ((messages == 'Miss') | (messages == 'Miss (already hit)')).sum()


# === File Paths ===
inputFile = 'D:\\Rouse\\My Documents\\GitHub\\RhythmGame\\AnalysisCode\\GameAnalysis\\Output\\allTapData.csv'
outputFolder = 'D:\\Rouse\\My Documents\\GitHub\\RhythmGame\\AnalysisCode\\GameAnalysis\\Output'

# === Load the Data ===
allData = pd.read_table(inputFile,
                        header=0,
                        sep=','
                        )

# === clean the data ===
allData['Angle'] = allData['Angle'].replace('[nan]', np.nan).astype(float)

# === aggregate by trial ===
# # this is the looped way to get the totals per trial
# subjectList = allData['Subject'].unique()
# for subject in subjectList:
#     currSubData = allData[allData['Subject'].str.match(subject)]
#     sessionList = currSubData['SessionNum'].unique()
#     for currSession in sessionList:
#         currSessionData = currSubData[currSubData['SessionNum'] == currSession]
#         trialList = currSubData['Trial'].unique()
#         for currTrial in trialList:
#             currTrialData = currSessionData[currSessionData['Trial'] == currTrial]
#             # get angle and vector of trial

# # this is the groupby approach to do the same thing!
outputGB = allData.groupby(["Subject", "SessionNum", "PhaseNum", "Trial"])
outputDataTrial = pd.DataFrame()

# = Calculated columns =
outputDataTrial['Date'] = outputGB['Date'].first()
outputDataTrial['meanAngle'] = outputGB['Angle'].apply(circ_mean)
outputDataTrial['firstAngle'] = outputGB['Angle'].first()
outputDataTrial['sin_mean'] = outputGB['Angle'].apply(average_sin)
outputDataTrial['cos_mean'] = outputGB['Angle'].apply(average_cos)
outputDataTrial['nTaps'] = outputGB['Angle'].count()  # number of taps
outputDataTrial['vectorLength'] = outputGB['Angle'].apply(circ_r)  # vector length
outputDataTrial['safeCount'] = outputGB['Message'].apply(safe_count)
outputDataTrial['missCount'] = outputGB['Message'].apply(miss_count)
outputDataTrial['safeProportion'] = (outputDataTrial['safeCount'] /
                                     (outputDataTrial['safeCount'] + outputDataTrial['missCount']))
# additional analyses

# = save results =
outputDataTrial.to_csv(os.path.join(outputFolder, 'trialSummaryData.csv'))

# === aggregate by session ===
outputSessionGB = outputDataTrial.reset_index().groupby(["Subject", "SessionNum", "PhaseNum"])
outputDataSession = pd.DataFrame()

# = Calculated columns =
outputDataSession['Date'] = outputSessionGB['Date'].first()
outputDataSession['meanSessionAngle'] = outputSessionGB.apply(lambda x: circ_mean(x['meanAngle'], x['vectorLength']),
                                                              include_groups=False)
outputDataSession['nTrials'] = outputSessionGB['Trial'].nunique()  # n trials
outputDataSession['meanVectorLength'] = outputSessionGB['vectorLength'].mean()  # vector length
# = save results =
outputDataSession.to_csv(os.path.join(outputFolder, 'sessionSummaryData.csv'))
