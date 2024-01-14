using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure;


namespace RaceTrac.Function
{
    public static class AzMonitorQueryAPI
    {
        [FunctionName("AzMonitorQueryAPI")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            
            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            /*
            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";*/

            
            string sStoreNumber=req.Query["storeNo"];
            sStoreNumber ??= data?.storeNo;
            string sDate=req.Query["incidentDate"];
            sDate ??= data?.incidentDate;

            // If we don't have both store # and incident date, then return error
            if(sStoreNumber == null || sStoreNumber.Length <1 || sDate == null || sDate.Length < 1)
            {
                log.LogError("Missing either store number or incident date.  Both are required.");
                return new BadRequestResult();
            }
            

            var clientID = Environment.GetEnvironmentVariable("Managed_Identity_Client_ID");
            
            log.LogInformation("Client id is {0}",clientID);
            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = clientID
            };
            
            string workspaceId = Environment.GetEnvironmentVariable("Log_Analytics_Workspace_ID");
            
            log.LogInformation("workspaceID = {0}",workspaceId);
            var client = new LogsQueryClient(new DefaultAzureCredential(credentialOptions));

            // build the KQL query
            // Include the store and date
            string kqlQuery = string.Format("jsn1971customlog_CL | where eventID  == \"{0}\" " +
            "| where TimeGenerated > startofday(datetime({1})) " +
            "| where TimeGenerated < endofday(datetime({1})) " +
            "| order by TimeGenerated asc",sStoreNumber,sDate);

            log.LogInformation("kqlQuery = {0}",kqlQuery);

            Response<LogsQueryResult> result = await client.QueryWorkspaceAsync(
            workspaceId,
            kqlQuery,
            QueryTimeRange.All);

            LogsTable table = result.Value.Table;

            var myJList = new List<Journey>();
            foreach (var row in table.Rows)
            {
                log.LogInformation($"{row["TimeGenerated"]} {row["stepName"]} {row["action"]}");
                Journey myJourney = new Journey();
                myJourney.details = string.Format($"{row["action"]}");
                int nStep = Int32.Parse($"{row["stepID"]}");
                myJourney.eventID = nStep;
                myJourney.dateTime = DateTime.Parse($"{row["TimeGenerated"]}");
                myJList.Add(myJourney);
            }


            //return new OkObjectResult(responseMessage);            

            /*
            
            for(int i=0;i<50;i++)
            {
                Journey myJourney = new Journey();
                myJourney.details = string.Format("Incident details for store {0} and date of {1}",sStoreNumber,sDate);
                myJourney.eventID= i+101;
                myJourney.dateTime = DateTime.Now;
                myJList.Add(myJourney);
            }*/
            

            return new OkObjectResult(myJList);
        }
    }
}
