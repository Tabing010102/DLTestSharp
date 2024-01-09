using DLTest;
using Grpc.Core;

namespace DLTestServer.Services
{
    public class DLTestService : DLTest.DLTest.DLTestBase
    {
        private readonly ILogger<DLTestService> _logger;
        public DLTestService(ILogger<DLTestService> logger)
        {
            _logger = logger;
        }

        public override Task<GetDLResponse> GetDLFiles(GetDLRequest request, ServerCallContext context)
        {
            return base.GetDLFiles(request, context);
        }
    }
}
