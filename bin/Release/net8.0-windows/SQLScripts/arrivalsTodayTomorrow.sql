DECLARE @ValorTaxaT INT, @Hoje DATE;

SET @Hoje = (SELECT pdate FROM proteluser.datum WHERE mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID});

-- Ajustando o ID correto para a tabela de divisão da taxa turística
SET @ValorTaxaT = (SELECT betrag FROM proteluser.splittab WHERE @Hoje BETWEEN fromdate AND todate AND stabnr = 22);

SELECT
    (SELECT
        proteluser.buch.buchnr AS 'ResNo',
        CAST(proteluser.buch.datumvon AS DATE) AS 'DateCI',
        CAST(proteluser.buch.datumbis AS DATE) AS 'DateCO',
        CASE WHEN proteluser.buch.reisenr > 0 THEN AGENCIA.name1 ELSE '' END AS 'Booker',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.name1 ELSE '' END AS 'Company',
        CASE WHEN proteluser.buch.gruppennr > 0 THEN GRUPO.name1 ELSE '' END AS 'Group',
        CASE WHEN proteluser.buch.sourcenr > 0 THEN OUTROS.name1 ELSE '' END AS 'Others',
        proteluser.zimmer.ziname AS 'Room',
        proteluser.buch.not1txt AS 'Notes',
	EMPRESA.kdnr as 'CompanyID',
        CASE WHEN proteluser.buch.firmennr > 0 THEN 1 ELSE 0 END AS 'hasCompanyVAT',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.landkz ELSE -1 END AS 'CompanyCountryID',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.land ELSE '' END AS 'CompanyCountryName',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.strasse ELSE '' END AS 'CompanyStreetAddress',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.plz ELSE '' END AS 'CompanyZipCode',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.ort ELSE '' END AS 'CompanyCity',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.region ELSE '' END AS 'CompanyState',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.vatno ELSE '' END AS 'CompanyVatNo',
        CASE WHEN proteluser.buch.firmennr > 0 THEN EMPRESA.email ELSE '' END AS 'CompanyEmail',
        CASE 
            WHEN proteluser.zistat.status = 1 THEN 'Cleaned' 
            WHEN proteluser.zistat.status = 2 THEN 'Dirty' 
            WHEN proteluser.zistat.status = 3 THEN 'Out Of Service' 
            WHEN proteluser.zistat.status = 4 THEN 'Checked' 
            WHEN proteluser.zistat.status = 5 THEN 'Touched' 
            WHEN proteluser.zistat.status = 6 THEN 'Cleaning in Progress'
            ELSE 'Cleaning Schedule' 
        END AS 'RoomStatus',
        proteluser.kat.kat AS 'RoomType',
        proteluser.buch.anzerw AS 'Adults',
        proteluser.buch.anzkin1 + proteluser.buch.anzkin2 + proteluser.buch.anzkin3 + proteluser.buch.anzkin4 AS 'Childs',
        proteluser.ptypgrp.gruppe AS 'RateCode',
        CASE 
            WHEN EXISTS (SELECT * FROM proteluser.varbuch WHERE proteluser.varbuch.buchnr = proteluser.buch.buchnr) 
            THEN CAST((SELECT SUM(proteluser.varbuch.preis) / DATEDIFF(DAY, proteluser.buch.datumvon, proteluser.buch.datumbis) 
                       FROM proteluser.varbuch WHERE proteluser.varbuch.buchnr = proteluser.buch.buchnr) AS DECIMAL(10,2)) 
            ELSE proteluser.buch.grundpreis 
        END AS 'Price',
        CASE 
            WHEN DATEDIFF(DAY, proteluser.buch.datumvon, proteluser.buch.datumbis) > 7 
            THEN @ValorTaxaT * proteluser.buch.anzerw * 7 
            ELSE @ValorTaxaT * proteluser.buch.anzerw * DATEDIFF(DAY, proteluser.buch.datumvon, proteluser.buch.datumbis) 
        END AS 'CityTax',
        CASE 
            WHEN EXISTS (SELECT * FROM proteluser.varbuch WHERE proteluser.varbuch.buchnr = proteluser.buch.buchnr) 
            THEN CAST((SELECT SUM(proteluser.varbuch.preis) 
                       FROM proteluser.varbuch WHERE proteluser.varbuch.buchnr = proteluser.buch.buchnr) AS DECIMAL(10,2)) 
            ELSE proteluser.buch.grundpreis 
        END AS 'Total'
    FOR JSON PATH) AS 'ReservationInfo',
    
    (SELECT
        (SELECT
            proteluser.buch.kundennr AS 'ProfileID',
            proteluser.kunden.anrede AS 'Salution',
            proteluser.kunden.name1 AS 'LastName',
            proteluser.kunden.vorname AS 'FirstName'
        FOR JSON PATH) AS 'GuestDetails',
        
        (SELECT
            proteluser.kunden.land AS 'Country',
            proteluser.kunden.strasse AS 'Street',
            proteluser.kunden.plz AS 'PostalCode',
            proteluser.kunden.ort AS 'City',
            proteluser.kunden.region AS 'Region'
        FOR JSON PATH) AS 'Address',
        
        (SELECT
            proteluser.kunden.gebdat AS 'DateOfBirth',
            natcode.land AS 'CountryOfBirth',
            N2.land AS 'Nationality',    
            proteluser.xdoctype.text AS 'IDDoc',
            proteluser.kunden.passnr AS 'NrDoc',
            proteluser.kunden.docvalid AS 'ExpDate',
            proteluser.kunden.issued AS 'Issue'
        FOR JSON PATH) AS 'PersonalID',
        
        (SELECT
            proteluser.kunden.email AS 'Email',
            proteluser.kunden.telefonnr AS 'PhoneNumber',
            proteluser.kunden.vatno AS 'VatNo'
        FOR JSON PATH) AS 'Contacts'
    FROM proteluser.kunden
    LEFT JOIN proteluser.sprache ON proteluser.sprache.nr = proteluser.kunden.sprache
    LEFT JOIN proteluser.xdoctype ON proteluser.xdoctype.ref = proteluser.kunden.doctype
    LEFT JOIN proteluser.natcode ON proteluser.natcode.codenr = proteluser.kunden.gebland
    LEFT JOIN proteluser.natcode AS N2 ON N2.codenr = proteluser.kunden.nat
    WHERE proteluser.kunden.kdnr = proteluser.buch.kundennr
    FOR JSON PATH) AS 'GuestInfo'
FROM proteluser.buch
INNER JOIN proteluser.kunden ON proteluser.kunden.kdnr = proteluser.buch.kundennr
LEFT JOIN proteluser.kunden AS AGENCIA ON AGENCIA.kdnr = proteluser.buch.reisenr
LEFT JOIN proteluser.kunden AS EMPRESA ON EMPRESA.kdnr = proteluser.buch.firmennr
LEFT JOIN proteluser.kunden AS GRUPO ON GRUPO.kdnr = proteluser.buch.gruppennr
LEFT JOIN proteluser.kunden AS OUTROS ON OUTROS.kdnr = proteluser.buch.sourcenr
INNER JOIN proteluser.zimmer ON proteluser.zimmer.zinr = proteluser.buch.zimmernr
INNER JOIN proteluser.kat ON proteluser.kat.katnr = proteluser.buch.katnr
INNER JOIN proteluser.zistat ON proteluser.zistat.zinr = proteluser.buch.zimmernr
INNER JOIN proteluser.ptypgrp ON proteluser.ptypgrp.ptgnr = proteluser.buch.preistypgr
WHERE proteluser.buch.mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID}
  AND proteluser.buch.buchstatus = 0
  AND proteluser.buch.resstatus NOT IN (3,7)
  AND proteluser.buch.datumvon = @Hoje
  AND proteluser.kat.zimmer = 1
FOR JSON PATH;
	