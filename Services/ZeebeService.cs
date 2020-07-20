using System;
using System.IO;
using System.Threading.Tasks;
using dotenv.net.Interfaces;
using fastJSON;
using Microsoft.Extensions.Logging;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Zeebe.Client.Impl.Builder;

namespace Cloudstarter.Services
{
    public interface IZeebeService
    {
        public Task<IDeployResponse> Deploy(string modelFilename);
        public Task<String> StartWorkflowInstance(string bpmProcessId);
        public void StartWorkers();
        public Task<ITopology> Status();

    }
    public class ZeebeService: IZeebeService
    {
        private readonly IZeebeClient _client;
        private readonly ILogger<ZeebeService> _logger;

        public ZeebeService(IEnvReader envReader, ILogger<ZeebeService> logger)
        {
            _logger = logger;
            var authServer = envReader.GetStringValue("ZEEBE_AUTHORIZATION_SERVER_URL"); 
            var clientId = envReader.GetStringValue("ZEEBE_CLIENT_ID");
            var clientSecret = envReader.GetStringValue("ZEEBE_CLIENT_SECRET");
            var zeebeUrl = envReader.GetStringValue("ZEEBE_ADDRESS");
            char[] port =
            {
                '4', '3', ':'
            };
            var audience = zeebeUrl?.TrimEnd(port);

            _client =
                ZeebeClient.Builder()
                    .UseGatewayAddress(zeebeUrl)
                    .UseTransportEncryption()
                    .UseAccessTokenSupplier(
                        CamundaCloudTokenProvider.Builder()
                            .UseAuthServer(authServer)
                            .UseClientId(clientId)
                            .UseClientSecret(clientSecret)
                            .UseAudience(audience)
                            .Build())
                    .Build();
        }

        public Task<ITopology> Status()
        {
            return _client.TopologyRequest().Send();
        }
        
        public async void StartWorkers()
        {
            await Task.Run(CreateGetTimeWorker);
        }
        public void CreateGetTimeWorker()
        {
            _createWorker("get-time", async (client, job) =>
            {
                _logger.LogInformation("Received job: " + job);
                await client.NewCompleteJobCommand(job.Key).Send();
            });    
        }
        
        public async Task<IDeployResponse> Deploy(string modelFilename)
        {
            var filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, "Resources", modelFilename);
            try
            {
                var deployment = await _client.NewDeployCommand().AddResourceFile(filename).Send();
                var res = deployment.Workflows[0];
                _logger.LogInformation("Deployed BPMN Model: " + res?.BpmnProcessId +
                                       " v." + res?.Version);
                return deployment;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                throw;
            }
        }
        
        public async Task<String> StartWorkflowInstance(string bpmProcessId)
        {
            _logger.LogInformation("Starting workflow instance...");
            var instance = await _client.NewCreateWorkflowInstanceCommand()
                .BpmnProcessId(bpmProcessId)
                .LatestVersion()
                .WithResult()
                .Send();
            var jsonParams = new JSONParameters {ShowReadOnlyProperties = true};
            return JSON.ToJSON(instance, jsonParams);
        }
        
        private void _createWorker(String jobType, JobHandler handleJob)
        {
            _client.NewWorker()
                .JobType(jobType)
                .Handler(handleJob)
                .MaxJobsActive(5)
                .Name(jobType)
                .PollInterval(TimeSpan.FromSeconds(50))
                .PollingTimeout(TimeSpan.FromSeconds(50))
                .Timeout(TimeSpan.FromSeconds(10))
                .Open();
        }
    }
}