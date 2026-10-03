namespace BBF.Shared.Contracts.Amp;

public class AmpInstance
{
    public string InstanceId { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string Module { get; set; } = "";
    public bool IsRunning { get; set; }
    public string? Endpoint { get; set; }

    public string GameType => Module switch
    {
        "Minecraft" => "Minecraft",
        "srcds" => "Source Engine",
        "GenericModule" => "Generic",
        "ADS" => "Manager",
        _ => Module
    };

    public string GameIcon => Module switch
    {
        "Minecraft" => "terrain",
        "ADS" => "dns",
        _ => "sports_esports"
    };
}

public class AmpInstanceStatus
{
    public int State { get; set; }
    public string? Uptime { get; set; }
    public double? Cpu { get; set; }
    public double? Memory { get; set; }
    public int ActiveUsers { get; set; }
    public int MaxUsers { get; set; }

    public string StateName => State switch
    {
        0 => "Stopped",
        5 => "Starting",
        10 => "Ready",
        20 => "Starting",
        30 => "Stopping",
        40 => "Installing",
        45 => "Updating",
        _ => "Unknown"
    };

    public bool IsOnline => State == 10;
}
