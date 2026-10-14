using System;

namespace AgduBugdu.Core.Licensing;

public interface ILicenseStorage
{
    LicenseRecord? LoadLicenseRecord();
    void SaveLicenseRecord(LicenseRecord record);
    void ClearLicenseRecord();
    DateTime GetOrCreateTrialStartDate();
    long GetLastKnownClockTicks();
    void UpdateLastKnownClockTicks(long ticks);
}
