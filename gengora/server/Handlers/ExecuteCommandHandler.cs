namespace BITS.Gengora.Server.Handlers;

/// <summary>
/// Handles workspace/executeCommand requests for starting and stopping the generator.
/// </summary>
public class ExecuteCommandHandler(IGeneratorService generatorService) : ExecuteCommandHandlerBase
{
    private readonly IGeneratorService _GeneratorService = generatorService;

    public override async Task<Unit> Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
    {
        var command = request.Command;

        if (command == Constants.Commands.GENERATOR_START || command == Constants.Commands.GENGORA_START)
        {
            await this._GeneratorService.StartGeneratorAsync(cancellationToken);
        }
        else if (command == Constants.Commands.GENERATOR_STOP || command == Constants.Commands.GENGORA_STOP)
        {
            await this._GeneratorService.StopGeneratorAsync(cancellationToken);
        }

        return Unit.Value;
    }

    protected override ExecuteCommandRegistrationOptions CreateRegistrationOptions(ExecuteCommandCapability capability, ClientCapabilities clientCapabilities)
    {
        return new ExecuteCommandRegistrationOptions
        {
            Commands = new Container<string>(Constants.Commands.ALL_COMMANDS)
        };
    }
}
