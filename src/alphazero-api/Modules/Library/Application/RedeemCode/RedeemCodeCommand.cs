using MediatR;
using ErrorOr;
using AlphaZero.Shared.Application;

namespace AlphaZero.Modules.Library.Application.RedeemCode;

public record RedeemCodeCommand(
    string RawCode,
    string? IpAddress = null,
    string? DeviceFingerprint = null) : ICommand<Success>;
