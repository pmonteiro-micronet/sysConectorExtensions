using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Data.SqlClient;

public class DatabaseWindowsService : ServiceBase
{
    private static string connectionString;
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

            // Ler a connection string do arquivo
            connectionString = ReadConnectionStringFromFile("connectionString.txt");

            // Testar conexão ao banco de dados
            TestDatabaseConnection();

            // Iniciar servidor HTTP/HTTPS
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

    private static string ReadConnectionStringFromFile(string filePath)
    {
        try
        {
            string absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
            return File.ReadAllText(absolutePath).Trim();
        }
        catch (Exception ex)
        {
            throw new Exception("Error reading connection string: " + ex.Message);
        }
    }

    private static void TestDatabaseConnection()
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            Log("Database connection successful.");
        }
    }

    private static void Log(string message)
    {
        string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service.log");
        File.AppendAllText(logFile, $"{DateTime.Now}: {message}{Environment.NewLine}");
    }

    private void StartHttpServer()
{
    try
    {
        // Ler a porta do arquivo
        int port = ReadPortFromFile("port.txt");

        listener = new HttpListener();

        // Configurar o prefixo HTTPS usando a porta especificada
        listener.Prefixes.Add($"http://+:{port}/"); // Escuta em todas as interfaces

        listenerThread = new Thread(() =>
        {
            try
            {
                listener.Start();
                Log($"HTTP Server started on port {port}. Listening for requests...");

                while (listener.IsListening)
                {
                    var context = listener.GetContext(); // Espera por requisições
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
            }
            catch (Exception ex)
            {
                Log($"HTTP Server error: {ex.Message}");
            }
        });

        listenerThread.IsBackground = true;
        listenerThread.Start();
    }
    catch (Exception ex)
    {
        Log($"Error starting HTTP Server: {ex.Message}");
    }
}

private static int ReadPortFromFile(string filePath)
{
    try
    {
        string absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
        string portString = File.ReadAllText(absolutePath).Trim();

        if (int.TryParse(portString, out int port) && port > 0 && port <= 65535)
        {
            return port;
        }
        else
        {
            throw new Exception("Invalid port number in file.");
        }
    }
    catch (Exception ex)
    {
        throw new Exception("Error reading port from file: " + ex.Message);
    }
}


    private void StopHttpServer()
    {
        if (listener != null)
        {
            listener.Stop();
            listener.Close();
        }

        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Join();
        }
    }

    private void HandleRequest(HttpListenerContext context)
{
    try
    {
        string urlPath = context.Request.Url.AbsolutePath.ToLower(); // Caminho da URL
        string responseMessage = "";

        if (urlPath == "/pp_xml_ckit_statementcheckouts")
        {
            // Verificar se o método é GET
            if (context.Request.HttpMethod == "GET")
            {
                // Obter o parâmetro "HotelID" da query string
                string hotelID = context.Request.QueryString["HotelID"];

                if (!string.IsNullOrEmpty(hotelID))
                {
                    try
                    {
                        // Executar o script SQL com o parâmetro HotelID
                        string jsonResult = ExecuteSqlScriptWithParameter(hotelID);

                        // Retornar o resultado
                        responseMessage = jsonResult;
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "application/json";
                    }
                    catch (Exception ex)
                    {
                        responseMessage = $"Erro ao executar o SQL: {ex.Message}";
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    responseMessage = "Erro: Parâmetro 'HotelID' não foi fornecido.";
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
            }
            else
            {
                responseMessage = "Método HTTP não suportado nesta rota.";
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
        }
        else if (urlPath == "/pp_xml_ckit_extratoconta")
        {
            // Verificar se o método é GET
            if (context.Request.HttpMethod == "GET")
            {
                // Obter os parâmetros "ResNumber" e "window" da query string
                string resNumber = context.Request.QueryString["ResNumber"];
                string window = context.Request.QueryString["window"];

                if (!string.IsNullOrEmpty(resNumber) && !string.IsNullOrEmpty(window))
                {
                    try
                    {
                        // Executar o script SQL com os parâmetros
                        string jsonResult = ExecuteSqlScriptWithParameters(resNumber, window);

                        // Retornar o resultado
                        responseMessage = jsonResult;
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "application/json";
                    }
                    catch (Exception ex)
                    {
                        responseMessage = $"Erro ao executar o SQL: {ex.Message}";
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    responseMessage = "Erro: Parâmetros 'ResNumber' e 'window' são obrigatórios.";
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
            }
            else
            {
                responseMessage = "Método HTTP não suportado nesta rota.";
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
        }
        else
        {
            responseMessage = "Rota não reconhecida.";
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }

        // Log da requisição
        Log($"Request received: {context.Request.HttpMethod} {context.Request.Url}");

        // Enviar a resposta
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseMessage);
        context.Response.ContentLength64 = responseBytes.Length;
        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        context.Response.OutputStream.Close();
    }
    catch (Exception ex)
    {
        Log($"Error handling request: {ex.Message}");
    }
}


private string ExecuteSqlScriptWithParameter(string hotelID)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "departuresTodayTomorrow.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);
    sqlScript = sqlScript.Replace("{STATEMENT_CHECKOUTS_WEBSERVICE.HotelID}", hotelID);

    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        connection.Open();

        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            using (SqlDataReader reader = command.ExecuteReader())
            {
                StringBuilder jsonResult = new StringBuilder();

                while (reader.Read())
                {
                    string jsonRaw = reader[0]?.ToString();
                    if (!string.IsNullOrEmpty(jsonRaw))
                    {
                        // Tratar para remover a chave JSON desnecessária
                        var cleanedJson = CleanJson(jsonRaw);
                        jsonResult.Append(cleanedJson);
                    }
                }

                return jsonResult.ToString();
            }
        }
    }
}

private string CleanJson(string jsonRaw)
{
    try
    {
        var jsonObject = System.Text.Json.JsonDocument.Parse(jsonRaw).RootElement;
        
        // Verifica se é uma única chave (o caso do JSON_F52E2B61-18A1-11d1)
        if (jsonObject.EnumerateObject().Count() == 1)
        {
            foreach (var element in jsonObject.EnumerateObject())
            {
                return element.Value.ToString(); // Retorna o valor puro
            }
        }
    }
    catch (Exception ex)
    {
        Log($"Erro ao processar JSON: {ex.Message}");
    }

    return jsonRaw; // Caso não consiga tratar
}


private string ExecuteSqlScriptWithParameters(string resNumber, string window)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "statement.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);
    sqlScript = sqlScript
        .Replace("{STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber}", resNumber)
        .Replace("{STATEMENT_EXTRACTOCONTA_WEBSERVICE.window}", window);

    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        connection.Open();

        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            using (SqlDataReader reader = command.ExecuteReader())
            {
                StringBuilder jsonResult = new StringBuilder();

                while (reader.Read())
                {
                    // Extrai o JSON bruto retornado pela consulta
                    string jsonRaw = reader[0]?.ToString();
                    if (!string.IsNullOrEmpty(jsonRaw))
                    {
                        // Limpa a chave extra e obtém apenas o valor do JSON
                        string cleanedJson = CleanJson(jsonRaw);
                        jsonResult.Append(cleanedJson);
                    }
                }

                return jsonResult.ToString();
            }
        }
    }
}



    public static void Main()
    {
        ServiceBase.Run(new DatabaseWindowsService());
    }
}
