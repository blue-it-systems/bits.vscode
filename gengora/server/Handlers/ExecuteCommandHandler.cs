using BITS.Gengora.Server.Services;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

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
        else if (command == Constants.Commands.GENGORA_PAUSE)
        {
            await this._GeneratorService.PauseGeneratorAsync(cancellationToken);
        }
        else if (command == Constants.Commands.GENGORA_SWITCH_PROJECT && request.Arguments != null && request.Arguments.Count > 0)
        {
            var projectPath = request.Arguments[0].ToString();
            if (!string.IsNullOrEmpty(projectPath))
            {
                await this._GeneratorService.SwitchProjectAsync(projectPath, cancellationToken);
            }
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
