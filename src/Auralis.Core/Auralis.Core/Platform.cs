namespace Auralis.Core;

public static class Platform
{
    public static readonly PlatformType PlatformType  =
        OperatingSystem.IsWindows() ? PlatformType.Windows :
        OperatingSystem.IsLinux() ? PlatformType.Linux :
        OperatingSystem.IsAndroid() ? PlatformType.Android :
        OperatingSystem.IsMacOS() ? PlatformType.MacOs :
        OperatingSystem.IsIOS() ? PlatformType.IOS : PlatformType.Unknown;

    public static readonly bool IsWindows = PlatformType == PlatformType.Windows;
    public static readonly bool IsLinux = PlatformType == PlatformType.Linux;
    public static readonly bool IsAndroid = PlatformType == PlatformType.Android;
    public static readonly bool IsIOS = PlatformType == PlatformType.IOS;
    public static readonly bool IsMacOS = PlatformType == PlatformType.MacOs;
}
