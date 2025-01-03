﻿using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Data.SqlClient;


public class DatabaseWindowsService : ServiceBase
{
    private static ServiceConfig config;
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

            // Carregar configurações do arquivo
            config = LoadConfiguration("config.json");

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

    private static ServiceConfig LoadConfiguration(string filePath)
{
    try
    {
        string absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
        string jsonContent = File.ReadAllText(absolutePath);
        ServiceConfig loadedConfig = JsonSerializer.Deserialize<ServiceConfig>(jsonContent);

        // Validar o caminho do PDF
        if (!Directory.Exists(loadedConfig.PdfSavePath))
        {
            throw new Exception($"O caminho para salvar PDFs '{loadedConfig.PdfSavePath}' é inválido ou não existe.");
        }

        return loadedConfig;
    }
    catch (Exception ex)
    {
        throw new Exception("Error loading configuration: " + ex.Message);
    }
}


    private static void TestDatabaseConnection()
    {
        using (SqlConnection connection = new SqlConnection(config.ConnectionString))
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
            listener = new HttpListener();

            // Configurar o prefixo HTTPS usando a porta especificada
            listener.Prefixes.Add($"http://+:{config.ServicePort}/"); // Escuta em todas as interfaces

            listenerThread = new Thread(() =>
            {
                try
                {
                    listener.Start();
                    Log($"HTTP Server started on port {config.ServicePort}. Listening for requests...");

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
        else if (urlPath == "/pp_xml_ckit_statementinhouses")
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
                        string jsonResult = ExecuteSqlScriptWithParameterInHouses(hotelID);

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
        else if (urlPath == "/valuesedited")
        {
            if (context.Request.HttpMethod == "POST")
            {
                try
                {
                    // Ler o corpo da requisição
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        string requestBody = reader.ReadToEnd();
                        
                        // Log para verificar os dados recebidos
                        Log($"Dados recebidos: {requestBody}");

                        // Validar os dados recebidos
                        if (!string.IsNullOrEmpty(requestBody))
                        {
                            responseMessage = "Dados processados com sucesso!";
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            responseMessage = "Erro: Corpo da requisição está vazio.";
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                }
                catch (Exception ex)
                {
                    responseMessage = $"Erro ao processar os dados: {ex.Message}";
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
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
        else if (urlPath == "/searchrecordid")
        {
            if (context.Request.HttpMethod == "GET")
            {
                // Obter o parâmetro "BuchID" da query string
                string buchID = context.Request.QueryString["BuchID"];

                if (!string.IsNullOrEmpty(buchID))
                {
                    try
                    {
                        // Executar o script SQL com o parâmetro BuchID
                        string jsonResult = ExecuteSqlScriptWithParameterSearchRecord(buchID);

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
                    responseMessage = "Erro: Parâmetro 'BuchID' não foi fornecido.";
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

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
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

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
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

private string ExecuteSqlScriptWithParameterSearchRecord(string buchID)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "searchID.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL 'searchID.sql' não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);
    sqlScript = sqlScript.Replace("{STATEMENT_INHOUSES_WEBSERVICE.RecordID}", buchID);

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
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
                        // Limpa e processa o JSON retornado
                        string cleanedJson = CleanJson(jsonRaw);
                        jsonResult.Append(cleanedJson);
                    }
                }

                return jsonResult.ToString();
            }
        }
    }
}

private string ExecuteSqlScriptWithParameterInHouses(string hotelID)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "inHousesTodayTomorrow.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);
    sqlScript = sqlScript.Replace("{STATEMENT_INHOUSES_WEBSERVICE.HotelID}", hotelID);

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
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
    string saveDir = config.PdfSavePath; // Usar o caminho especificado na configuração

    // Verificar e garantir que o nome do arquivo tenha a extensão .pdf
    if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
    {
        fileName += ".pdf";
    }

    string filePath = Path.Combine(saveDir, fileName);

    // Salvar o arquivo PDF
    File.WriteAllBytes(filePath, Convert.FromBase64String(base64Content));

    // Extrair o profileID do nome do arquivo
    string profileID = ExtractProfileID(fileName);

    // Log com o profileID
    Log($"PDF saved at {filePath} | Profile ID: {profileID}");

    // Fazer a requisição GET para o endereço especificado
    SendPathToEndpoint(profileID, filePath);

    return filePath;
}

// Método para extrair o profileID
private string ExtractProfileID(string fileName)
{
    // Usar expressão regular para encontrar a sequência de números no formato esperado
    var match = Regex.Match(fileName, @"RegistrationForm_profileID_(\d+)");
    if (match.Success)
    {
        return match.Groups[1].Value; // Retorna apenas o número
    }
    return "Unknown"; // Retorno padrão caso o formato não seja encontrado
}

// Método para enviar a requisição GET
private void SendPathToEndpoint(string profileID, string filePath)
{
    try
    {
        using (HttpClient client = new HttpClient())
        {
            // Montar o URL
            string url = $"http://192.168.145.5:91/pp_xml_ckit_registrationform?GuestID={profileID}&FilePath={Uri.EscapeDataString(filePath)}";

            // Fazer a requisição GET
            HttpResponseMessage response = client.GetAsync(url).Result;

            // Validar a resposta
            if (response.IsSuccessStatusCode)
            {
                Log($"Request successful for Profile ID: {profileID}. Response: {response.StatusCode}");
            }
            else
            {
                Log($"Request failed for Profile ID: {profileID}. Response: {response.StatusCode}");
            }
        }
    }
    catch (Exception ex)
    {
        Log($"Error during GET request: {ex.Message}");
    }
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

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
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

// Classe para armazenar configurações
public class ServiceConfig
{
    public string ConnectionString { get; set; }
    public int ServicePort { get; set; }
    public string PdfSavePath { get; set; } // Novo campo para caminho do PDF
}
