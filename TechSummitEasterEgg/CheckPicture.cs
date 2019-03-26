using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TechSummitEasterEgg
{
    public static class CheckPicture
    {
        // REPLACE CONSTANTS HERE ----------------------------------------------

        // If you set this constant to True, we will tweet your success from 
        // the Microsoft TechSummit account!!
        private const bool PublishSuccessOnTwitter = false; // set to true so a tweet gets posted

        // Enter your Twitter name. 
        // This name will be published to Twitter if PublishSuccessOnTwitter is set to true. 
        private const string Twitter = "[YOUR TWITTER NAME]";

        // Enter the key phrase that you copied from the disk's readme file.
        private const string SecretKey = "[KEY PHRASE FROM THE DISK]";
        
        // Enter the name of the storage account you created.
        private const string StorageAccountName = "[STORAGE ACCOUNT NAME]";

        // Enter the subscription key and the endpoint of the Face API cognitive service you created.
        private const string FaceSubscriptionKey = "[COGNITIVE SERVICE KEY]";
        private const string FaceEndpoint = "[COGNITIVE SERVICE ENDPOINT]";

        // ------------------------------------------------------------------

        private const string UrlBaseFunction = "https://lbswisstechsummit19.azurewebsites.net/api/check-id?code=Av6VBZfLgEJ6DO20av2YNTZeVXdPmrdHNU0YvoGprf1b4n0paGQAMw==";
		
        private const string ContainerName = "tech-summit";
        private const string ContainerResultName = "tech-summit-result";

        private static readonly FaceAttributeType[] faceAttributes
            = { FaceAttributeType.Emotion };

        [FunctionName("CheckPicture")]
        public static async Task Run(
            [BlobTrigger(
                "tech-summit/{name}", 
                Connection = "AzureWebJobsStorage")]
            Stream myBlob, 
            string name, 
            ILogger log,
            [Blob("tech-summit-result/{name}.txt", FileAccess.Write)]
            Stream resultStream)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            // Recognize the face emotion

            string result;

            log.LogInformation("Recognizing the emotion");

            var faceClient = new FaceClient(
                new ApiKeyServiceClientCredentials(FaceSubscriptionKey),
                new DelegatingHandler[] { })
            {
                Endpoint = FaceEndpoint
            };

            IList<DetectedFace> faceList = null;
            Exception error = null;

            try
            {
                faceList
                    = await faceClient.Face.DetectWithStreamAsync(
                        myBlob,
                        true,
                        false,
                        faceAttributes);
            }
            catch (Exception ex)
            {
                log.LogError("There was an error", ex);
                error = ex;
            }

            if (error != null)
            {
                result = "There was an error:\r\n"
                    + error.Message;

                WriteResult(resultStream, result);
                return;
            }

            if (faceList == null
                || faceList.Count != 1)
            {
                result = "No faces, or more than one face detected:";
                log.LogInformation(result);
                WriteResult(resultStream, result);
                return;
            }

            if (faceList[0].FaceAttributes.Emotion.Happiness < 0.5)
            {
                result = "You should be happier than that!! Try again!!";
                log.LogInformation(result);
                WriteResult(resultStream, result);
                return;
            }

            log.LogInformation("You seem very happy, let's check further");

            var parameters = new
            {
                SecretKey,
                BlobUrl = $"https://{StorageAccountName}.blob.core.windows.net/{ContainerName}/{name}",
                TwitterName = Twitter,
                PublishSuccessOnTwitter,
                SubscriptionId = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME"),
                HostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")
            };

            var json = JsonConvert.SerializeObject(parameters);

            // Communicate with the base function

            var client = new HttpClient();
            var content = new StringContent(json);

            log.LogInformation("Checking your submission...");

            var response = await client.PostAsync(UrlBaseFunction, content);

            // Check response

            var responseStream = await response.Content.ReadAsStreamAsync();

            using (var reader = new StreamReader(responseStream))
            {
                result = reader.ReadToEnd();
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                result = $"Status code doesn't indicate success: {result}";
                log.LogInformation(result);
                WriteResult(resultStream, result);
                return;
            }
        
            // Write to the output blob
            
            WriteResult(resultStream, result);

            var resultUrl = $"https://{StorageAccountName}.blob.core.windows.net/{ContainerResultName}/{name}.txt";
            log.LogInformation($"Success!! Check {resultUrl} for details.");
        }
    
        private static void WriteResult(Stream resultStream, string result)
        {
            using (var writer = new StreamWriter(resultStream))
            {
                writer.Write(result);
            }
        }
    }
}
