﻿using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Data.SqlClient;
using System.Text.Json;


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
                        string jsonResult = ExecuteSqlScriptWithParameterCheckouts(hotelID);

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
        else if (urlPath == "/pp_xml_ckit_statementcheckins")
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
                        string jsonResult = ExecuteSqlScriptWithParameterArrivals(hotelID);

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
        else if (urlPath == "/registration_form_base64" && context.Request.HttpMethod == "POST")
{
    // Obter o nome do arquivo do cabeçalho
    string fileName = context.Request.Headers["FileName"];
    if (string.IsNullOrEmpty(fileName))
    {
        responseMessage = "Cabeçalho 'FileName' é obrigatório.";
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
    else
    {
        // Ler o conteúdo do corpo (base64)
        using (var reader = new StreamReader(context.Request.InputStream))
        {
            string base64Content = reader.ReadToEnd();
            if (!string.IsNullOrEmpty(base64Content))
            {
                responseMessage = SaveBase64Pdf(base64Content, fileName);
            }
            else
            {
                responseMessage = "O corpo da requisição deve conter o conteúdo base64.";
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }
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
                        string jsonResult = ExecuteSqlScriptWithParametersExtrato(resNumber, window);

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


private string ExecuteSqlScriptWithParameterCheckouts(string hotelID)
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

private string ExecuteSqlScriptWithParameterArrivals(string hotelID)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "arrivalsTodayTomorrow.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);
    sqlScript = sqlScript.Replace("{STATEMENT_CHECKINS_WEBSERVICE.HotelID}", hotelID);

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

        // Verifica o tipo do elemento raiz
        if (jsonObject.ValueKind == JsonValueKind.Object)
        {
            // Processar o caso de objeto
            if (jsonObject.EnumerateObject().Count() == 1)
            {
                foreach (var element in jsonObject.EnumerateObject())
                {
                    return element.Value.ToString();
                }
            }
            return jsonObject.ToString(); // Retorna o objeto como está
        }
        else if (jsonObject.ValueKind == JsonValueKind.Array)
        {
            // Caso seja um array, retorna como string
            return jsonObject.ToString();
        }
    }
    catch (Exception ex)
    {
        Log($"Erro ao processar JSON: {ex.Message}");
    }

    return jsonRaw; // Retorna o JSON original em caso de erro
}


private string SaveBase64Pdf(string base64Content, string fileName)
{
    string saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PDFs");
    Directory.CreateDirectory(saveDir);

    // Verificar e garantir que o nome do arquivo tenha a extensão .pdf
    if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
    {
        fileName += ".pdf";
    }

    string filePath = Path.Combine(saveDir, fileName);

    File.WriteAllBytes(filePath, Convert.FromBase64String(base64Content));
    Log($"PDF saved at {filePath}");
    return filePath;
}



private string ExecuteSqlScriptWithParametersExtrato(string resNumber, string window)
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