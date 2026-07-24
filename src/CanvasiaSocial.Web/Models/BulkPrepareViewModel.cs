using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Application.Campaigns;

namespace CanvasiaSocial.Web.Models;

public sealed class BulkPrepareViewModel
{
    public List<Guid> ProductIds { get; set; } = [];
    public IReadOnlyList<ProductListItem> Products { get; set; } = [];
    public IReadOnlyList<SocialAccountOption> SocialAccounts { get; set; } = [];
    public string Name { get; set; } = string.Empty;
    public Platform Platform { get; set; } = Platform.Instagram;
    public Guid? SocialAccountId { get; set; }
    public CampaignMode Mode { get; set; } = CampaignMode.RequireApproval;
    public DateTime StartLocal { get; set; } = DateTime.Today.AddDays(1).AddHours(9);
    public int IntervalMinutes { get; set; } = 60;
    public int DailyLimit { get; set; } = 10;
    public TimeOnly AllowedStartTime { get; set; } = new(9, 0);
    public TimeOnly AllowedEndTime { get; set; } = new(21, 0);
    public bool IncludePrice { get; set; } = true;
    public bool IncludeProductLink { get; set; } = true;
}
