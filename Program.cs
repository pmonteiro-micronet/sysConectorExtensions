using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Data.SqlClient;
using System.Text.Json;

public class DatabaseWindowsService : ServiceBase
{
    private string connectionString;
    private HttpListener listener;
    private Thread listenerThread;

    public DatabaseWindowsService()
    {
        this.ServiceName = "DatabaseWindowsService";
        this.CanStop = true;
        this.CanPauseAndContinue = true;
        this.AutoLog = true;
    }

    protected override void OnStart(string[] args)
    {
        try
        {
            Log("Service is starting...");

            // Ler connection string
            connectionString = ReadConnectionStringFromFile("connectionString.txt");
            TestDatabaseConnection();

            // Iniciar o servidor HTTP
            StartHttpServer();

            Log("Service started successfully.");
        }
        catch (Exception ex)
        {
            Log($"Error during service start: {ex.Message}");
        }
    }

    protected override void OnStop()
    {
        Log("Service is stopping...");
        StopHttpServer();
        Log("Service stopped.");
    }

    private string ReadConnectionStringFromFile(string filePath)
    {
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
        return File.ReadAllText(fullPath).Trim();
    }

    private void TestDatabaseConnection()
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            Log("Database connection successful.");
        }
    }

    private void StartHttpServer()
    {
        int port = ReadPortFromFile("port.txt");
        listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{port}/");

        listenerThread = new Thread(() =>
        {
            listener.Start();
            Log($"HTTP Server started on port {port}");

            while (listener.IsListening)
            {
                var context = listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
        });
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    private void StopHttpServer()
    {
        listener?.Stop();
        listenerThread?.Join();
    }

    private int ReadPortFromFile(string filePath)
    {
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
        return int.Parse(File.ReadAllText(fullPath).Trim());
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            string urlPath = context.Request.Url.AbsolutePath.ToLower();
            string responseMessage = "";

            if (urlPath == "/pp_xml_ckit_statementcheckouts" && context.Request.HttpMethod == "GET")
            {
                string hotelID = context.Request.QueryString["HotelID"];
                responseMessage = !string.IsNullOrEmpty(hotelID)
                    ? ExecuteSqlScriptWithParameter("departuresTodayTomorrow.sql", hotelID)
                    : "Parâmetro 'HotelID' é obrigatório.";
            }
            else if (urlPath == "/pp_xml_ckit_statementcheckins" && context.Request.HttpMethod == "GET")
            {
                string hotelID = context.Request.QueryString["HotelID"];
                responseMessage = !string.IsNullOrEmpty(hotelID)
                    ? ExecuteSqlScriptWithParameter("arrivalsTodayTomorrow.sql", hotelID)
                    : "Parâmetro 'HotelID' é obrigatório.";
            }
            else if (urlPath == "/registration_form_base64" && context.Request.HttpMethod == "POST")
{
    using (var reader = new StreamReader(context.Request.InputStream))
    {
        string requestBody = reader.ReadToEnd();
        var requestData = JsonSerializer.Deserialize<JsonElement>(requestBody);

        if (requestData.TryGetProperty("pdfBase64", out var pdfBase64Element) &&
            requestData.TryGetProperty("fileName", out var fileNameElement))
        {
            string pdfBase64 = pdfBase64Element.GetString();
            string fileName = fileNameElement.GetString();

            if (!string.IsNullOrEmpty(pdfBase64) && !string.IsNullOrEmpty(fileName))
            {
                responseMessage = SaveBase64Pdf(pdfBase64, fileName);
            }
            else
            {
                responseMessage = "Parâmetros 'pdfBase64' e 'fileName' são obrigatórios.";
            }
        }
        else
        {
            responseMessage = "Parâmetros 'pdfBase64' e 'fileName' são obrigatórios.";
        }
    }
}

            else if (urlPath == "/pp_xml_ckit_extratoconta" && context.Request.HttpMethod == "GET")
            {
                string resNumber = context.Request.QueryString["ResNumber"];
                string window = context.Request.QueryString["window"];
                responseMessage = (!string.IsNullOrEmpty(resNumber) && !string.IsNullOrEmpty(window))
                    ? ExecuteSqlScriptWithParameters("statement.sql", resNumber, window)
                    : "Parâmetros 'ResNumber' e 'window' são obrigatórios.";
            }
            else
            {
                responseMessage = "Rota não reconhecida.";
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            SendResponse(context, responseMessage);
        }
        catch (Exception ex)
        {
            Log($"Error handling request: {ex.Message}");
        }
    }

    private void SendResponse(HttpListenerContext context, string message)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(message);
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }

    private string ExecuteSqlScriptWithParameter(string sqlFileName, string hotelID)
    {
        string sqlScript = LoadSqlScript(sqlFileName);
        sqlScript = sqlScript.Replace("{HotelID}", hotelID);

        using (SqlConnection connection = new SqlConnection(connectionString))
        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            connection.Open();
            using (SqlDataReader reader = command.ExecuteReader())
            {
                return ReadJsonResult(reader);
            }
        }
    }

    private string ExecuteSqlScriptWithParameters(string sqlFileName, string resNumber, string window)
    {
        string sqlScript = LoadSqlScript(sqlFileName)
            .Replace("{ResNumber}", resNumber)
            .Replace("{window}", window);

        using (SqlConnection connection = new SqlConnection(connectionString))
        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            connection.Open();
            using (SqlDataReader reader = command.ExecuteReader())
            {
                return ReadJsonResult(reader);
            }
        }
    }

    private string LoadSqlScript(string sqlFileName)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", sqlFileName);
        return File.ReadAllText(path);
    }

    private string ReadJsonResult(SqlDataReader reader)
    {
        StringBuilder jsonResult = new StringBuilder();
        while (reader.Read())
        {
            string jsonRaw = reader[0]?.ToString();
            jsonResult.Append(jsonRaw);
        }
        return jsonResult.ToString();
    }

    private string SaveBase64Pdf(string base64Content, string fileName)
{
    string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PDFs");
    Directory.CreateDirectory(saveDir);

    string filePath = Path.Combine(saveDir, fileName);

    File.WriteAllBytes(filePath, Convert.FromBase64String(base64Content));
    Log($"PDF saved at {filePath}");
    return filePath;
}


    private void Log(string message)
    {
        string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service.log");
        File.AppendAllText(logFile, $"{DateTime.Now}: {message}{Environment.NewLine}");
    }

    public static void Main()
    {
        ServiceBase.Run(new DatabaseWindowsService());
    }
}
