using System;
using NLog;
using Semver;

namespace CarbonCI
{
    
    public static class SemVersionHelpers
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static SemVersion incrementAlpha(this SemVersion oldVersion)
        {
            SemVersion newVer;
            
            if (oldVersion.Prerelease == "")
            {
                newVer = new SemVersion(oldVersion.Major, oldVersion.Minor, oldVersion.Patch+1, "alpha");
            }
            else if (!oldVersion.Prerelease.Contains("."))
            {
                newVer = new SemVersion(oldVersion.Major, oldVersion.Minor, oldVersion.Patch, "alpha.1");
                
            }
            else if (oldVersion.Prerelease.ToLower().Contains("alpha."))
            {
                var currentPreNumber = oldVersion.Prerelease.ToLower().Split('.')[1];
                var curNum = int.Parse(currentPreNumber);
                newVer = new SemVersion(oldVersion.Major, oldVersion.Minor, oldVersion.Patch, $"alpha.{curNum+1}");
            }
            else
            {
                return incrementPatch(oldVersion);
            }
            return newVer;
        }
        public static int getAlphaNumber(this SemVersion oldVersion)
        {

            if (oldVersion.Prerelease == "")
            {
                return 0;
            }
            else if (!oldVersion.Prerelease.Contains("."))
            {
                return 1;

            }
            else if (oldVersion.Prerelease.ToLower().Contains("alpha."))
            {
                var currentPreNumber = oldVersion.Prerelease.ToLower().Split('.')[1];
                var curNum = int.Parse(currentPreNumber);
                return curNum + 1;
            }
            else
            {
                return 0;
            }
        }
        public static SemVersion incrementPatch(this SemVersion oldVersion)
        {
            SemVersion newVer;
            if (oldVersion.Prerelease != "")
            {
                newVer = new SemVersion(oldVersion.Major, oldVersion.Minor, oldVersion.Patch, "", "");
            }
            else
            {
                newVer = new SemVersion(oldVersion.Major, oldVersion.Minor, oldVersion.Patch+1, "", "");
            }
            return newVer;
        }
        public static SemVersion incrementMinor(this SemVersion oldVersion)
        {
            var newVer = new SemVersion(oldVersion.Major, oldVersion.Minor+1, 0, "", "");
            return newVer;
        }
        public static SemVersion incrementMajor(this SemVersion oldVersion)
        {
            var newVer = new SemVersion(oldVersion.Major+1, 0, 0, "", "");
            return newVer;
        }
    }
}