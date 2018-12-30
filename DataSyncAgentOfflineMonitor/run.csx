#r "SendGrid"
#r "System.Data"

using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Text;
using Microsoft.Azure.WebJobs.Host;
using SendGrid.Helpers.Mail;

public static SendGridMessage Run(TimerInfo myTimer, string myInputBlob, out string myOutputBlob, ILogger log)
{
    string SyncDbServer = Environment.GetEnvironmentVariable("SyncDbServer");
    string SyncDbDatabase = Environment.GetEnvironmentVariable("SyncDbDatabase");
    string SyncDbUser = Environment.GetEnvironmentVariable("SyncDbUser");
    string SyncDbPassword = Environment.GetEnvironmentVariable("SyncDbPassword");
    string ToAddress = Environment.GetEnvironmentVariable("DataSyncAgentOfflineMonitorNotifyAddress");
    string dateTimeFormat = "yyyy-MM-dd HH:mm";
    SendGridMessage message = null;

    //Get last check datetime
    DateTime lastCheck;
    if (!DateTime.TryParse(myInputBlob, out lastCheck))
    {
        log.LogInformation($"Was not possible to parse myInputBlob");
        lastCheck = DateTime.UtcNow.AddHours(-1);
    };
    log.LogInformation($"lastCheck: {lastCheck}");
    myOutputBlob = lastCheck.ToString();

    try
    {
        SqlConnection conn = new SqlConnection(string.Format("Server=tcp:{0}.database.windows.net,1433;Initial Catalog={1};Persist Security Info=False;User ID={2};Password={3};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;", SyncDbServer, SyncDbDatabase, SyncDbUser, SyncDbPassword));
        SqlDataReader rdr = null;

        conn.Open();
        rdr = cmd.ExecuteReader();
        StringBuilder sb = new StringBuilder();
        cmd = new SqlCommand("SELECT [name] FROM [dss].[agent] WHERE [lastalivetime] < dateadd(minute, -15, GETDATE())", conn);
        rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            string AgentName = rdr.IsDBNull(0) ? "" : rdr.GetString(0);
            sb.AppendLine($"<tr><th style='background-color: #E74C3C;'>Agent {AgentName} is offline</th></tr>");            
        }

        log.LogInformation($"sb.Length: {sb.Length}");
        log.LogInformation($"sb.ToString: {sb.ToString()}");
        
        if (sb.Length > 0)
        {
            StringBuilder emailContentSB = new StringBuilder();
            emailContentSB.Append(@"<style>
table { border-collapse: collapse; width: 100%;}
th, td { text-align: left; padding: 8px;}
tr:nth-child(even){background-color: #f2f2f2}
th { color: white;}
</style>
<div style='overflow-x:auto;'>");
            emailContentSB.Append($"<h3>Data Sync agent offline alert at {DateTime.UtcNow.ToString(dateTimeFormat)} UTC</h3>");
            emailContentSB.Append($"Last check at {lastCheck.ToString("s")}</h3><br/>");
            emailContentSB.Append(@"<table >");
            emailContentSB.Append(sb.ToString());
            emailContentSB.Append(@"</table></div>");

            message = new SendGridMessage() { Subject = "Data Sync agent offline alert" };
            message.AddTo(ToAddress);
            message.AddContent("text/html", emailContentSB.ToString());
            myOutputBlob = DateTime.UtcNow.ToString();
        }
    }
    catch (Exception ex)
    {
        log.LogError($"Exception: {ex.Message}");
    }
    finally
    {
        if (rdr != null) { rdr.Close(); }
        if (conn != null) { conn.Close(); }
    }
    return message;
}