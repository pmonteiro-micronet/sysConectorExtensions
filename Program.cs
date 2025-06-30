using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Data.SqlClient;
using System.IO.Compression;
using System.Collections.Concurrent;

public class DatabaseWindowsService : ServiceBase
{
    private static ServiceConfig config;
    private HttpListener listener;
    private Thread listenerThread;
    private Thread requestProcessorThread;
    private ConcurrentQueue<HttpListenerContext> requestQueue;
    private ManualResetEventSlim queueNotifier;

    public DatabaseWindowsService()
    {
        this.ServiceName = "DatabaseWindowsService";
        this.CanStop = true;
        this.CanPauseAndContinue = false;
        this.AutoLog = true;

        requestQueue = new ConcurrentQueue<HttpListenerContext>();
        queueNotifier = new ManualResetEventSlim(false);
    }

    private bool ValidateToken(HttpListenerContext context)
{
    string token = context.Request.Headers["Authorization"];
    return token == "q4vf9p8n4907895f7m8d24m75c2q947m2398c574q9586c490q756c98q4m705imtugcfecvrhym04capwz3e2ewqaefwegfiuoamv4ros2nuyp0sjc3iutow924bn5ry943utrjmi"; // Replace with your actual token validation logic
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

            // Start the request processor thread
            requestProcessorThread = new Thread(ProcessRequests);
            requestProcessorThread.IsBackground = true;
            requestProcessorThread.Start();

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

        // Stop the request processor thread
        queueNotifier.Set(); // Signal the thread to exit
        if (requestProcessorThread != null && requestProcessorThread.IsAlive)
        {
            requestProcessorThread.Join();
        }

        Log("Service stopped.");
    }

    private static ServiceConfig LoadConfiguration(string filePath)
{
    while (true)
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
            Log($"Error loading configuration: {ex.Message}. Retrying in 1 minute...");
            Thread.Sleep(TimeSpan.FromMinutes(1));
        }
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
            listener.Prefixes.Add($"http://+:{config.ServicePort}/");
            listenerThread = new Thread(() =>
            {
                listener.Start();
                Log($"HTTP Server started on port {config.ServicePort}.");
                while (listener.IsListening)
                {
                    try
                    {
                        var context = listener.GetContext();
                        requestQueue.Enqueue(context);
                        queueNotifier.Set();
                    }
                    catch (HttpListenerException) { break; }
                    catch (Exception ex) { Log($"Listener error: {ex.Message}"); }
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

    private void ProcessRequests()
    {
        while (true)
        {
            queueNotifier.Wait();
            while (requestQueue.TryDequeue(out var context))
            {
                try
                {
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    Log($"Error processing request: {ex.Message}");
                    try { context.Response.StatusCode = 500; context.Response.Close(); } catch { }
                }
            }
            queueNotifier.Reset();
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            if (!ValidateToken(context))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.Close();
                return;
            }

            string urlPath = context.Request.Url.AbsolutePath.ToLower();
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

                // Deserializar o JSON recebido
                var requestData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);


                if (requestData != null && 
                    requestData.ContainsKey("registerID") && 
                    requestData.ContainsKey("editedEmail") && 
                    requestData.ContainsKey("editedVAT"))
                {
                    string registerID = requestData["registerID"];
                    string editedEmail = requestData["editedEmail"];
                    string editedVAT = requestData["editedVAT"];

                    // Validar os parâmetros recebidos
                    if (!string.IsNullOrEmpty(registerID))
                    {
                        // Carregar o script SQL do arquivo
                        string sqlTemplate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "updateEmailVat.sql");

// Chamada correta usando os parâmetros
var result = ExecuteSqlToUpdateEmailVAT(sqlTemplate, registerID, editedEmail, editedVAT);



                        // Retornar sucesso com os dados atualizados
                        responseMessage = System.Text.Json.JsonSerializer.Serialize(result);
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                    }
                    else
                    {
                        responseMessage = "Erro: Parâmetros inválidos ou vazios.";
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    responseMessage = "Erro: Parâmetros ausentes no corpo da requisição.";
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

                        // Fazer POST para o endpoint /api/submitReservation
                        bool postSuccess = PostToSubmitReservation(jsonResult);

                        if (postSuccess)
                        {
                            responseMessage = jsonResult;
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.ContentType = "application/json";
                        }
                        else
                        {
                            responseMessage = "Erro ao enviar os dados para o endpoint de reserva.";
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        }
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
        else if (urlPath == "/healthcheck")
{
    if (context.Request.HttpMethod == "GET")
    {
        try
        {
            // Retornar o status do serviço
            var healthStatus = new
            {
                Status = "Running",
                Timestamp = DateTime.UtcNow.ToString("o") // Formato ISO 8601
            };

            // Serializar o status como JSON
            responseMessage = System.Text.Json.JsonSerializer.Serialize(healthStatus);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
        }
        catch (Exception ex)
        {
            responseMessage = $"Erro ao verificar o status: {ex.Message}";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
    }
    else
    {
        responseMessage = "Método HTTP não suportado nesta rota.";
        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
    }
}else if (urlPath == "/nationalities")
{
    if (context.Request.HttpMethod == "GET")
    {
        try
        {
            // Executar o script SQL para obter as nacionalidades
            string jsonResult = ExecuteSqlScriptNationalities();

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
        responseMessage = "Método HTTP não suportado nesta rota.";
        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
    }
}
else if (urlPath == "/insertcompany")
{
    if (context.Request.HttpMethod == "POST")
    {
        try
        {
            // Captura os parâmetros dos headers, definindo " " como padrão para os campos opcionais
            string companyName = context.Request.Headers["CompanyName"];
            string resNo = context.Request.Headers["ResNo"];
            string countryID = context.Request.Headers["CountryID"] ?? " ";
            string countryName = context.Request.Headers["CountryName"] ?? " ";
            string streetAddress = context.Request.Headers["StreetAddress"] ?? " ";
            string zipCode = context.Request.Headers["ZipCode"] ?? " ";
            string city = context.Request.Headers["City"] ?? " ";
            string state = context.Request.Headers["State"] ?? " ";
            string vatNo = context.Request.Headers["VatNo"] ?? " ";
            string email = context.Request.Headers["Email"] ?? " ";

            // Verifica se os campos obrigatórios estão preenchidos
            if (string.IsNullOrWhiteSpace(companyName))
            {
                throw new ArgumentException("CompanyName é obrigatório.");
            }
            if (string.IsNullOrWhiteSpace(resNo))
            {
                throw new ArgumentException("ResNo é obrigatório.");
            }

            // Executar o script SQL e obter o ID da empresa inserida
            int insertedId = ExecuteSqlScriptInsertCompany(companyName, countryID, countryName, streetAddress, zipCode, city, state, vatNo, email, resNo);

            // Retornar resposta no formato JSON
            var jsonResponse = new { CompanyID = insertedId };
            string responseJson = System.Text.Json.JsonSerializer.Serialize(jsonResponse);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                writer.Write(responseJson);
            }
        }
        catch (ArgumentException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            var errorResponse = new { error = $"Erro nos parâmetros: {ex.Message}" };
            string errorJson = System.Text.Json.JsonSerializer.Serialize(errorResponse);
            context.Response.ContentType = "application/json";
            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                writer.Write(errorJson);
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            var errorResponse = new { error = $"Erro ao executar o SQL: {ex.Message}" };
            string errorJson = System.Text.Json.JsonSerializer.Serialize(errorResponse);
            context.Response.ContentType = "application/json";
            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                writer.Write(errorJson);
            }
        }
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        var errorResponse = new { error = "Método HTTP não suportado nesta rota." };
        string errorJson = System.Text.Json.JsonSerializer.Serialize(errorResponse);
        context.Response.ContentType = "application/json";
        using (var writer = new StreamWriter(context.Response.OutputStream))
        {
            writer.Write(errorJson);
        }
    }
}



else if (urlPath == "/updatecompany")
{
    if (context.Request.HttpMethod == "POST")
    {
        try
        {
            // Captura os parâmetros dos headers, definindo " " como padrão para os campos opcionais
            string companyID = context.Request.Headers["CompanyID"];
            string companyName = context.Request.Headers["CompanyName"];
            string countryID = context.Request.Headers["CountryID"] ?? " ";
            string countryName = context.Request.Headers["CountryName"] ?? " ";
            string streetAddress = context.Request.Headers["StreetAddress"] ?? " ";
            string zipCode = context.Request.Headers["ZipCode"] ?? " ";
            string city = context.Request.Headers["City"] ?? " ";
            string state = context.Request.Headers["State"] ?? " ";
            string vatNo = context.Request.Headers["VatNo"] ?? " ";
            string email = context.Request.Headers["Email"] ?? " ";

            // Verifica se os campos obrigatórios estão preenchidos
            if (string.IsNullOrWhiteSpace(companyID))
            {
                throw new ArgumentException("CompanyID é obrigatório.");
            }
            if (string.IsNullOrWhiteSpace(companyName))
            {
                throw new ArgumentException("CompanyName é obrigatório.");
            }

            // Executar o script SQL para atualizar a empresa
            ExecuteSqlScriptUpdateCompany(companyID, companyName, countryID, countryName, streetAddress, zipCode, city, state, vatNo, email);

            responseMessage = "Empresa atualizada com sucesso!";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }
        catch (ArgumentException ex)
        {
            responseMessage = $"Erro nos parâmetros: {ex.Message}";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
        catch (Exception ex)
        {
            responseMessage = $"Erro ao executar o SQL: {ex.Message}";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
    }
    else
    {
        responseMessage = "Método HTTP não suportado nesta rota.";
        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
    }
}

else if (urlPath == "/updatecheckin")
{
    if (context.Request.HttpMethod == "POST")
    {
        try
        {
            // Captura o parâmetro do header
            string resNo = context.Request.Headers["resNo"];

            // Verifica se o campo obrigatório está vazio
            if (string.IsNullOrWhiteSpace(resNo))
            {
                throw new ArgumentException("resNo é obrigatório.");
            }

            // Executar o script SQL para atualizar a reserva
            ExecuteSqlScriptUpdateResStat(resNo);

            responseMessage = "Status da reserva atualizado com sucesso!";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }
        catch (ArgumentException ex)
        {
            responseMessage = $"Erro nos parâmetros: {ex.Message}";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
        catch (Exception ex)
        {
            responseMessage = $"Erro ao executar o SQL: {ex.Message}";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
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

private bool PostToSubmitReservation(string jsonData)
{
    try
    {
        using (HttpClient client = new HttpClient())
        {
            // Configurar o URL do endpoint
            string url = "https://extensions.mypms.pt/api/submitReservation";

            // Criar o conteúdo da requisição
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            // Enviar a requisição POST
            HttpResponseMessage response = client.PostAsync(url, content).Result;

            // Verificar o status da resposta
            if (response.IsSuccessStatusCode)
            {
                Log("POST para /api/submitReservation enviado com sucesso.");
                return true;
            }
            else
            {
                Log($"Erro no POST para /api/submitReservation: {response.StatusCode}");
                return false;
            }
        }
    }
    catch (Exception ex)
    {
        Log($"Erro ao enviar POST para /api/submitReservation: {ex.Message}");
        return false;
    }
}

private string ExecuteSqlScriptNationalities()
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "nationalities.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);

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

private int ExecuteSqlScriptInsertCompany(string companyName, string countryID, string countryName, string streetAddress, string zipCode, string city, string state, string vatNo, string email, string resNo)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "insertCompany.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);

    // Substituir placeholders pelos valores reais
    sqlScript = sqlScript.Replace("<CompanyName_string80>", companyName)
                         .Replace("<CountryID_CodeNrColumnFromNatcode_integer>", countryID)
                         .Replace("<CountryID_LandColumnFromNatcode_string80>", countryName)
                         .Replace("<StreetAddress_string80>", streetAddress)
                         .Replace("<ZipCode_string17>", zipCode)
                         .Replace("<City_string50>", city)
                         .Replace("<state_string80>", state)
                         .Replace("<vatNO_string30>", vatNo)
                         .Replace("<emailaddress_string75>", email)
                         .Replace("<IDReserva>", resNo);

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
    {
        connection.Open();
        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            object result = command.ExecuteScalar();
            if (result != null && int.TryParse(result.ToString(), out int insertedId))
            {
                return insertedId;
            }
            else
            {
                throw new Exception("Falha ao obter o ID do registro inserido.");
            }
        }
    }
}


private void ExecuteSqlScriptUpdateCompany(string companyID, string companyName, string countryID, string countryName, string streetAddress, string zipCode, string city, string state, string vatNo, string email)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "updateCompany.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);

    // Substituir placeholders pelos valores reais
    sqlScript = sqlScript.Replace("<CompanyName_string80>", companyName)
                         .Replace("<CountryID_CodeNrColumnFromNatcode_integer>", countryID)
                         .Replace("<CountryID_LandColumnFromNatcode_string80>", countryName)
                         .Replace("<StreetAddress_string80>", streetAddress)
                         .Replace("<ZipCode_string17>", zipCode)
                         .Replace("<City_string50>", city)
                         .Replace("<state_string80>", state)
                         .Replace("<vatNO_string30>", vatNo)
                         .Replace("<emailaddress_string75>", email)
                         .Replace("<ProfileID_integer>", companyID);

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
    {
        connection.Open();
        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            command.ExecuteNonQuery();
        }
    }
}

private void ExecuteSqlScriptUpdateResStat(string resNo)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "updateResStat.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);

    // Substituir o placeholder pelo valor real
    sqlScript = sqlScript.Replace("<ReservaID>", resNo);

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
    {
        connection.Open();
        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            command.ExecuteNonQuery();
        }
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

    try
    {
        // Converter a string Base64 para um array de bytes
        byte[] compressedBytes = Convert.FromBase64String(base64Content);

        // Descomprimir os bytes usando GZipStream
        byte[] decompressedBytes;
        using (MemoryStream compressedStream = new MemoryStream(compressedBytes))
        using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
        using (MemoryStream decompressedStream = new MemoryStream())
        {
            gzipStream.CopyTo(decompressedStream);
            decompressedBytes = decompressedStream.ToArray();
        }

        // Salvar os bytes descomprimidos como um arquivo PDF
        File.WriteAllBytes(filePath, decompressedBytes);

        // Extrair o profileID do nome do arquivo
        string profileID = ExtractProfileID(fileName);
        string TC = ExtractTC(fileName);
        string DPP = ExtractDPP(fileName);

        // Log com o profileID
        Log($"PDF saved at {filePath} | Profile ID: {profileID} | TC: {TC} | DPP: {DPP}");

        // Verificar se DPP é igual a 0 e executar o script SQL
        if (DPP == "0")
        {
            ExecuteSqlToUpdateTerms(profileID);
        }

        // Fazer a requisição GET para o endereço especificado
        SendPathToEndpoint(profileID, filePath);

        return filePath;
    }
    catch (Exception ex)
    {
        Log($"Erro ao salvar PDF: {ex.Message}");
        throw;
    }
}

private void ExecuteSqlToUpdateTerms(string profileID)
{
    string sqlScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLScripts", "updateTerms.sql");

    if (!File.Exists(sqlScriptPath))
    {
        throw new FileNotFoundException("O arquivo SQL 'updateTerms.sql' não foi encontrado.");
    }

    string sqlScript = File.ReadAllText(sqlScriptPath);
    sqlScript = sqlScript.Replace("{STATEMENT_UPDATETERMS_WEBSERVICE.GuestID}", profileID);

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
    {
        connection.Open();

        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            command.ExecuteNonQuery();
        }
    }
}

// Método para extrair o profileID
private string ExtractProfileID(string fileName)
{
    var match = Regex.Match(fileName, @"ProfileID_(\d+)");
    return match.Success ? match.Groups[1].Value : "Unknown";
}

private string ExtractTC(string fileName)
{
    var match = Regex.Match(fileName, @"_TC_(\d+)");
    return match.Success ? match.Groups[1].Value : "Unknown";
}

private string ExtractDPP(string fileName)
{
    var match = Regex.Match(fileName, @"_DPP_(\d+)");
    return match.Success ? match.Groups[1].Value : "Unknown";
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

private string ExecuteSqlToUpdateEmailVAT(string sqlTemplatePath, string registerID, string editedEmail, string editedVAT)
{
    // Carrega o script SQL do arquivo
    string sqlScript = File.ReadAllText(sqlTemplatePath);

    using (SqlConnection connection = new SqlConnection(config.ConnectionString))
    {
        connection.Open();

        using (SqlCommand command = new SqlCommand(sqlScript, connection))
        {
            // Adiciona os parâmetros
            command.Parameters.AddWithValue("@RegisterID", registerID);
            command.Parameters.AddWithValue("@EditedEmail", editedEmail);
            command.Parameters.AddWithValue("@EditedVAT", editedVAT);

            // Executa o comando e lê o resultado
            using (SqlDataReader reader = command.ExecuteReader())
            {
                StringBuilder jsonResult = new StringBuilder();

                while (reader.Read())
                {
                    string jsonRaw = reader[0]?.ToString();
                    if (!string.IsNullOrEmpty(jsonRaw))
                    {
                        jsonResult.Append(CleanJson(jsonRaw));
                    }
                }

                return jsonResult.ToString();
            }
        }
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
        try{
        ServiceBase.Run(new DatabaseWindowsService());
        }
        catch (Exception ex)
        {
            Log($"Error during service execution: {ex.Message}");
        }
    }
}

// Classe para armazenar configurações
public class ServiceConfig
{
    public string ConnectionString { get; set; }
    public int ServicePort { get; set; }
    public string PdfSavePath { get; set; } // Novo campo para caminho do PDF
}
