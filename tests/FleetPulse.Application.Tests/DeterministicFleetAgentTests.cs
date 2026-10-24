using FleetPulse.Application;

namespace FleetPulse.Application.Tests;

public sealed class DeterministicFleetAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Says_when_the_fleet_is_empty()
    {
        var agent = Create(new FakeFleetQuery([], VehicleTrack.Missing()));

        var reply = await agent.AskAsync("¿Dónde está la flota?", CancellationToken.None);

        Assert.False(reply.UsesLanguageModel);
        Assert.Equal(DeterministicFleetAgent.ModeName, reply.Mode);
        Assert.Contains("No hay vehículos", reply.Answer);
    }

    [Fact]
    public async Task Reports_only_vehicles_returned_by_the_query()
    {
        var vehicleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var vehicles = new[]
        {
            new VehicleState(vehicleId, Guid.NewGuid(), 1, Now, 4.65, -74.05)
        };
        var agent = Create(new FakeFleetQuery(vehicles, VehicleTrack.Missing()));

        var reply = await agent.AskAsync("Lista la flota", CancellationToken.None);

        Assert.Contains(vehicleId.ToString(), reply.Answer);
        Assert.Contains("4.65", reply.Answer);
        Assert.DoesNotContain("999", reply.Answer);
    }

    [Fact]
    public async Task Distinguishes_a_missing_vehicle_from_an_empty_track()
    {
        var missingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var knownId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var agent = Create(new FakeFleetQuery([], VehicleTrack.Found([]), knownId));

        var missing = await agent.AskAsync($"Telemetría de {missingId}", CancellationToken.None);
        var empty = await agent.AskAsync($"Telemetría de {knownId}", CancellationToken.None);

        Assert.Contains("no está registrado", missing.Answer);
        Assert.Contains("no tiene posiciones", empty.Answer);
    }

    [Fact]
    public async Task Does_not_invent_incidents()
    {
        var agent = Create(new FakeFleetQuery([], VehicleTrack.Missing()));

        var reply = await agent.AskAsync("¿Hay alertas o incidentes?", CancellationToken.None);

        Assert.Contains("No hay un registro de incidentes", reply.Answer);
        Assert.DoesNotContain("latitud", reply.Answer, StringComparison.OrdinalIgnoreCase);
    }

    private static DeterministicFleetAgent Create(FakeFleetQuery query) =>
        new(new FleetAgentTools(query, new FixedClock(Now)));

    private sealed class FakeFleetQuery : IFleetQuery
    {
        private readonly IReadOnlyList<VehicleState> _vehicles;
        private readonly VehicleTrack _knownTrack;
        private readonly Guid _knownVehicleId;

        public FakeFleetQuery(IReadOnlyList<VehicleState> vehicles, VehicleTrack knownTrack, Guid? knownVehicleId = null)
        {
            _vehicles = vehicles;
            _knownTrack = knownTrack;
            _knownVehicleId = knownVehicleId ?? Guid.Empty;
        }

        public Task<IReadOnlyList<VehicleState>> ListCurrentAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult(_vehicles);

        public Task<VehicleTrack> ListPositionsAsync(
            Guid vehicleId,
            DateTimeOffset from,
            DateTimeOffset to,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(vehicleId == _knownVehicleId ? _knownTrack : VehicleTrack.Missing());
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedClock(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
