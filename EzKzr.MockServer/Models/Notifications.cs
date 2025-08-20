namespace EzKzr.MockServer.Models;

public sealed class CreateNotification
{
    public KzrDotaz? ZadostInfo { get; set; }
    public NotificationRequest? ZadostData { get; set; }
}

public sealed class NotificationRequest
{
    public string? System { get; set; }
    public string? Typ { get; set; }
    public string? Kriteria { get; set; }
    public string? Kanal { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; }
    public string System { get; set; } = "";
    public string Typ { get; set; } = "";
    public string? Kriteria { get; set; }
    public string Kanal { get; set; } = "internal";
    public DateTime Vytvoreno { get; set; }
    public string Stav { get; set; } = "aktivni";
}
