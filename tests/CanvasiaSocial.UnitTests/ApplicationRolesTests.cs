using CanvasiaSocial.Application.Common.Security;

namespace CanvasiaSocial.UnitTests;

public sealed class ApplicationRolesTests
{
    [Fact]
    public void All_ContainsExpectedRoles()
    {
        Assert.Equal(
            ["Admin", "Editor", "Approver", "Viewer"],
            ApplicationRoles.All);
    }
}
