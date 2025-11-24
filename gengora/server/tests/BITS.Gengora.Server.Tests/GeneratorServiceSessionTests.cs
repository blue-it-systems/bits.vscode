using System;
using System.IO;
using System.Reflection;
using Xunit;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using BITS.Gengora.Server;
using BITS.Gengora.Server.Services;

namespace BITS.Gengora.Server.Tests
{
    public class GeneratorServiceSessionTests
    {
        private GeneratorService CreateService(Mock<ILanguageServerFacade> langMock, string workspaceRoot)
        {
            var gm = new GeneratorManager(workspaceRoot);
            var pm = new ProcessManager();
            var om = new ObservationManager(workspaceRoot);
            return new GeneratorService(gm, pm, om, langMock.Object);
        }

        [Fact]
        public void When_OwnsGenerator_And_SessionMatches_ForwardsMessage()
        {
            var lang = new Mock<ILanguageServerFacade>(MockBehavior.Strict);
            // Accept any SendNotification with the generator/generated method
            lang.Setup(l => l.SendNotification(It.Is<string>(s => s == "generator/generated"), It.IsAny<object>()));

            var service = CreateService(lang, Path.GetTempPath());

            // Set private fields to pretend we own the generator and have a session id
            var ownsField = typeof(GeneratorService).GetField("_ownsGenerator", BindingFlags.Instance | BindingFlags.NonPublic);
            var sessField = typeof(GeneratorService).GetField("_ownedGeneratorSessionId", BindingFlags.Instance | BindingFlags.NonPublic);
            ownsField!.SetValue(service, true);
            sessField!.SetValue(service, "session-abc");

            var method = typeof(GeneratorService).GetMethod("HandleGeneratorStdoutLine", BindingFlags.Instance | BindingFlags.NonPublic)!;

            string json = "{\"method\":\"generator/generated\",\"params\":{\"sessionId\":\"session-abc\",\"project\":\"/tmp/proj\",\"created\":[\"/tmp/proj/file1.txt\"]}}";

            method.Invoke(service, new object[] { json });

            lang.Verify(l => l.SendNotification("generator/generated", It.IsAny<object>()), Times.Once());
        }

        [Fact]
        public void When_OwnsGenerator_And_SessionMismatch_IgnoresMessage()
        {
            var lang = new Mock<ILanguageServerFacade>(MockBehavior.Strict);
            // Strict - any unexpected call will cause test failure

            var service = CreateService(lang, Path.GetTempPath());

            var ownsField = typeof(GeneratorService).GetField("_ownsGenerator", BindingFlags.Instance | BindingFlags.NonPublic);
            var sessField = typeof(GeneratorService).GetField("_ownedGeneratorSessionId", BindingFlags.Instance | BindingFlags.NonPublic);
            ownsField!.SetValue(service, true);
            sessField!.SetValue(service, "owned-session");

            var method = typeof(GeneratorService).GetMethod("HandleGeneratorStdoutLine", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // message from a different session
            string json = "{\"method\":\"generator/generated\",\"params\":{\"sessionId\":\"other-session\",\"project\":\"/tmp/proj\",\"created\":[\"/tmp/proj/file1.txt\"]}}";

            method.Invoke(service, new object[] { json });

            // Verify no notifications forwarded
            lang.Verify(l => l.SendNotification(It.IsAny<string>(), It.IsAny<object>()), Times.Never());
        }

        [Fact]
        public void When_OwnsGenerator_NoSessionInMessage_AcceptsMessage()
        {
            var lang = new Mock<ILanguageServerFacade>(MockBehavior.Strict);
            lang.Setup(l => l.SendNotification(It.Is<string>(s => s == "generator/generated"), It.IsAny<object>()));

            var service = CreateService(lang, Path.GetTempPath());

            var ownsField = typeof(GeneratorService).GetField("_ownsGenerator", BindingFlags.Instance | BindingFlags.NonPublic);
            var sessField = typeof(GeneratorService).GetField("_ownedGeneratorSessionId", BindingFlags.Instance | BindingFlags.NonPublic);
            ownsField!.SetValue(service, true);
            sessField!.SetValue(service, "owned-session");

            var method = typeof(GeneratorService).GetMethod("HandleGeneratorStdoutLine", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // message without a sessionId (backward compatibility path)
            string json = "{\"method\":\"generator/generated\",\"params\":{\"project\":\"/tmp/proj\",\"created\":[\"/tmp/proj/file1.txt\"]}}";

            method.Invoke(service, new object[] { json });

            lang.Verify(l => l.SendNotification("generator/generated", It.IsAny<object>()), Times.Once());
        }
    }
}
