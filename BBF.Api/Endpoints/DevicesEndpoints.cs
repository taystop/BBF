using BBF.Api.Auth;
using BBF.Api.Services;
using BBF.Shared.Contracts.Devices;

namespace BBF.Api.Endpoints;

public static class DevicesEndpoints
{
    public static void MapDevicesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices").RequireAuthorization();

        group.MapPost("/register", async (
            RegisterDeviceRequest request, DeviceTokenService devices, ICurrentUserContext currentUser, CancellationToken ct) =>
        {
            await devices.RegisterAsync(currentUser.UserId, request.Token, request.Platform, ct);
            return Results.NoContent();
        });

        group.MapDelete("/register", async (
            string token, DeviceTokenService devices, ICurrentUserContext currentUser, CancellationToken ct) =>
        {
            await devices.UnregisterAsync(currentUser.UserId, token, ct);
            return Results.NoContent();
        });
    }
}
