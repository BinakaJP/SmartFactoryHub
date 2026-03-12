using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Integration.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Notification.API.Data;
using Notification.API.Dtos;
using Notification.API.Models;

namespace Integration.Tests;

/// <summary>
/// Each test creates its own factory (+ isolated InMemory DB) for full test isolation.
/// </summary>
public class NotificationApiTests : IDisposable
{
    private readonly NotificationApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationApiTests()
    {
        _factory = new NotificationApiFactory();
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedNotificationAsync(string notificationType = "Alert", bool isRead = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notification = new NotificationLog
        {
            NotificationType = notificationType,
            EquipmentId      = "eq-notify-001",
            EquipmentName    = "Test Equipment",
            Message          = "Integration test message",
            Severity         = "Warning",
            Channels         = "SignalR",
            IsRead           = isRead
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification.Id;
    }

    // ── GET /api/notifications ────────────────────────────────────────────────

    [Fact]
    public async Task GetRecent_EmptyDatabase_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NotificationDto>>();
        body.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetRecent_WithTwoNotifications_ReturnsBoth()
    {
        await SeedNotificationAsync("Alert");
        await SeedNotificationAsync("EquipmentStatus");

        var response = await _client.GetAsync("/api/notifications?count=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NotificationDto>>();
        body.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecent_CountParameter_LimitsResults()
    {
        for (int i = 0; i < 5; i++)
            await SeedNotificationAsync();

        var response = await _client.GetAsync("/api/notifications?count=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NotificationDto>>();
        body!.Count.Should().BeLessThanOrEqualTo(2);
    }

    // ── GET /api/notifications/summary ───────────────────────────────────────

    [Fact]
    public async Task GetSummary_EmptyDatabase_ReturnsAllZeroCounts()
    {
        var response = await _client.GetAsync("/api/notifications/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<NotificationSummaryDto>();
        body.Should().NotBeNull();
        body!.Total.Should().Be(0);
        body.Unread.Should().Be(0);
    }

    [Fact]
    public async Task GetSummary_WithUnreadNotification_ReportsUnreadCount()
    {
        await SeedNotificationAsync("Alert", isRead: false);
        await SeedNotificationAsync("Alert", isRead: true);

        var response = await _client.GetAsync("/api/notifications/summary");
        var body = await response.Content.ReadFromJsonAsync<NotificationSummaryDto>();

        body!.Total.Should().Be(2);
        body.Unread.Should().Be(1);
    }

    // ── PATCH /api/notifications/{id}/read ────────────────────────────────────

    [Fact]
    public async Task MarkRead_ExistingNotification_Returns204()
    {
        var id = await SeedNotificationAsync(isRead: false);

        var response = await _client.PatchAsync($"/api/notifications/{id}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MarkRead_NonExistent_Returns404()
    {
        var response = await _client.PatchAsync($"/api/notifications/{Guid.NewGuid()}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/notifications/read-all ─────────────────────────────────────

    [Fact(Skip = "NotificationService.MarkAllAsReadAsync uses ExecuteUpdateAsync which is not supported by the EF Core InMemory provider")]
    public async Task MarkAllRead_WithUnreadNotifications_Returns204()
    {
        await SeedNotificationAsync(isRead: false);
        await SeedNotificationAsync(isRead: false);

        var response = await _client.PostAsync("/api/notifications/read-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(Skip = "NotificationService.MarkAllAsReadAsync uses ExecuteUpdateAsync which is not supported by the EF Core InMemory provider")]
    public async Task MarkAllRead_EmptyDatabase_Returns204()
    {
        var response = await _client.PostAsync("/api/notifications/read-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
