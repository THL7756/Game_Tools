namespace TableTool.Core.Models;

[Flags]
public enum ExportTarget
{
    None = 0,
    Client = 1,
    Server = 2,
    Both = Client | Server
}
