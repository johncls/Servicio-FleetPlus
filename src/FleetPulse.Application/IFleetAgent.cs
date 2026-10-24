namespace FleetPulse.Application;

public interface IFleetAgent
{
    Task<AgentReply> AskAsync(string question, CancellationToken cancellationToken);
}
