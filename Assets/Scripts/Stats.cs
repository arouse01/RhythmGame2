using System.Collections.Generic;
using UnityEngine;

public static class Stats 
{
    

    public static double CircMean(List<double> angleList, bool returnRad = true)
    {
        double sinSum = 0.0;
        double cosSum = 0.0;

        foreach (double angle in angleList)
        {
            sinSum += System.Math.Sin(angle);
            cosSum += System.Math.Cos(angle);
        }

        if (returnRad)
        {
            return System.Math.Atan2(sinSum, cosSum);
        }
        else
        {
            return System.Math.Atan2(sinSum, cosSum) * (180.0 / System.Math.PI); // Converted to degrees
        }

    }

    public static double CircVectorLength(List<double> angleList)
    {
        double sinSum = 0.0;
        double cosSum = 0.0;

        foreach (double angle in angleList)
        {
            sinSum += System.Math.Sin(angle);
            cosSum += System.Math.Cos(angle);
        }

        return System.Math.Sqrt(sinSum * sinSum + cosSum * cosSum) / angleList.Count;
    }
}
