DECLARE @COUNTER int
 
-- Incrementa 1 unidade ao último ID guardado dentro da tabela de contadores
select @COUNTER = kdnr+1 from proteluser.kundennr(tablockx);
-- Atualiza o valor obtido no último comando dentro da coluna kdnr da tabela de contadores
update proteluser.kundennr set kdnr=@COUNTER
 
-- Insere registo dentro da tabela dos Profiles
INSERT INTO proteluser.kunden
(
               kdnr,
               name1,
               landkz,
               land,
               strasse,
               plz,
               ort,
               region,
               nat,
               email   
)
values
(
               @COUNTER,
               '<CompanyName_string80>',
               '<CountryID_CodeNrColumnFromNatcode_integer>',
               '<CountryID_LandColumnFromNatcode_string80>',        
               '<StreetAddress_string80>',
               '<ZipCode_string17>',
               '<City_string50>',
               '<state_string80>',
               '<vatNO_string30>',
               '<emailaddress_string75>'
)

update proteluser.buch set firmennr = @COUNTER where buchnr = '<IDReserva>'

-- Retorna o ID gerado
SELECT @COUNTER AS InsertedID;