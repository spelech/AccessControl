namespace AccessControl.Engine.Services;

public class AppriseOptions
{
    public const string SectionName = "Apprise";

    public string Url { get; set; } = "http://10.0.0.10:8000/notify/apprise";
    public bool Enabled { get; set; } = true;
}
