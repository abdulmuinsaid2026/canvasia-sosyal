namespace CanvasiaSocial.Application.Common.Security;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Approver = "Approver";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Editor, Approver, Viewer];
}
