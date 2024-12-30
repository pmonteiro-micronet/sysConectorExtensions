SELECT 
    CAST(proteluser.buch.datumvon AS DATE) AS 'DateCI',
    CAST(proteluser.buch.datumbis AS DATE) AS 'DateCO',
    proteluser.kunden.name1 AS 'LastName',
    proteluser.kunden.vorname AS 'FirstName',
    CASE WHEN proteluser.buch.reisenr > 0 THEN AGENCIA.name1 ELSE '' END AS 'Booker',
    CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.name1 ELSE '' END AS 'Company',
    CASE WHEN proteluser.buch.gruppennr > 0 THEN GRUPO.name1 ELSE '' END AS 'Group',
    CASE WHEN proteluser.buch.sourcenr > 0 THEN OUTROS.name1 ELSE '' END AS 'Others',
    proteluser.zimmer.ziname AS 'Room',
    proteluser.buch.not1txt AS 'Notes'
FROM proteluser.buch
INNER JOIN proteluser.kunden ON proteluser.kunden.kdnr = proteluser.buch.kundennr
LEFT JOIN proteluser.kunden AS AGENCIA ON AGENCIA.kdnr = proteluser.buch.reisenr
LEFT JOIN proteluser.kunden AS EMPRESA ON EMPRESA.kdnr = proteluser.buch.firmennr
LEFT JOIN proteluser.kunden AS GRUPO ON GRUPO.kdnr = proteluser.buch.gruppennr
LEFT JOIN proteluser.kunden AS OUTROS ON OUTROS.kdnr = proteluser.buch.sourcenr
INNER JOIN proteluser.zimmer ON proteluser.zimmer.zinr = proteluser.buch.zimmernr
INNER JOIN proteluser.kat ON proteluser.kat.katnr = proteluser.buch.katnr
WHERE proteluser.buch.buchnr = {STATEMENT_INHOUSES_WEBSERVICE.RecordID}
FOR JSON PATH;
