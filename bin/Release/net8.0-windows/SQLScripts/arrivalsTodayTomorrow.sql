declare @ValorTaxaT int, @Hoje date
 
set @Hoje = (select pdate from proteluser.datum where mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID})
-- temos de alterar o stabnr para o ID que representa a tabela de divisão relativo à taxa turistica
set @ValorTaxaT = (select betrag from proteluser.splittab where @Hoje between fromdate and todate and stabnr=22)

select
    (select
        proteluser.buch.buchnr as 'ResNo',
        cast(proteluser.buch.datumvon as date) as 'DateCI',
        cast(proteluser.buch.datumbis as date) as 'DateCO',
        case when proteluser.buch.reisenr > 0 then AGENCIA.name1 else '' end as 'Booker',
        case when proteluser.buch.firmennr > 0 then EMPRESA.name1 else '' end as 'Company',
        case when proteluser.buch.gruppennr > 0 then GRUPO.name1 else '' end as 'Group',
        case when proteluser.buch.sourcenr > 0 then OUTROS.name1 else '' end as 'Others',
        proteluser.zimmer.ziname as 'Room',
        proteluser.buch.not1txt as 'Notes',
        case 
            when proteluser.zistat.status = 1 then 'Cleaned' 
            when proteluser.zistat.status = 2 then 'Dirty' 
            when proteluser.zistat.status = 3 then 'Out Of Service' 
            when proteluser.zistat.status = 4 then 'Checked' 
            when proteluser.zistat.status = 5 then 'Touched' 
            when proteluser.zistat.status = 6 then 'Cleaning in Progress'
            else 'Cleaning Schedule' 
        end as 'RoomStatus',
        proteluser.kat.kat as 'RoomType',
        proteluser.buch.anzerw as 'Adults',
        proteluser.buch.anzkin1 + proteluser.buch.anzkin2 + proteluser.buch.anzkin3 + proteluser.buch.anzkin4 as 'Childs',
        proteluser.ptypgrp.gruppe as 'RateCode',
        case 
            when exists (select * from proteluser.varbuch where proteluser.varbuch.buchnr = proteluser.buch.buchnr) 
            then cast((select sum(proteluser.varbuch.preis) / DATEDIFF(day, proteluser.buch.datumvon, proteluser.buch.datumbis) 
                       from proteluser.varbuch where proteluser.varbuch.buchnr = proteluser.buch.buchnr) as decimal(10,2)) 
            else proteluser.buch.grundpreis 
        end as 'Price',
        case 
            when DATEDIFF(day, proteluser.buch.datumvon, proteluser.buch.datumbis) > 7 
            then @ValorTaxaT * proteluser.buch.anzerw * 7 
            else @ValorTaxaT * proteluser.buch.anzerw * DATEDIFF(day, proteluser.buch.datumvon, proteluser.buch.datumbis) 
        end as 'CityTax',
        case 
            when exists (select * from proteluser.varbuch where proteluser.varbuch.buchnr = proteluser.buch.buchnr) 
            then cast((select sum(proteluser.varbuch.preis) 
                       from proteluser.varbuch where proteluser.varbuch.buchnr = proteluser.buch.buchnr) as decimal(10,2)) 
            else proteluser.buch.grundpreis 
        end as 'Total'
    FOR JSON PATH) as 'ReservationInfo',
    (Select
        (select
            proteluser.buch.kundennr as 'ProfileID',
            proteluser.kunden.anrede as 'Salution',
            proteluser.kunden.name1 as 'LastName',
            proteluser.kunden.vorname as 'FirstName'
        FOR JSON PATH) as 'GuestDetails',
        (select
            proteluser.kunden.land as 'Country',
            proteluser.kunden.strasse as 'Street',
            proteluser.kunden.plz as 'PostalCode',
            proteluser.kunden.ort as 'City',
            proteluser.kunden.region as 'Region'
        FOR JSON PATH) as 'Address',
        (Select
            proteluser.kunden.gebdat as 'DateOfBirth',
            natcode.land as 'CountryOfBirth',
            N2.land as 'Nationality',    
            proteluser.xdoctype.text as 'IDDoc',
            proteluser.kunden.passnr as 'NrDoc',
            proteluser.kunden.docvalid as 'ExpDate',
            proteluser.kunden.issued as 'Issue'
        FOR JSON PATH) as 'PersonalID',
        (select
            proteluser.kunden.email as 'Email',
            proteluser.kunden.telefonnr as 'PhoneNumber',
            proteluser.kunden.vatno as 'VatNo'
        FOR JSON PATH) as 'Contacts'
    from proteluser.kunden
    left join proteluser.sprache on proteluser.sprache.nr = proteluser.kunden.sprache
    left join proteluser.xdoctype on proteluser.xdoctype.ref = proteluser.kunden.doctype
    left join proteluser.natcode on proteluser.natcode.codenr = proteluser.kunden.gebland
    left join proteluser.natcode as N2 on N2.codenr = proteluser.kunden.nat
    where proteluser.kunden.kdnr = proteluser.buch.kundennr
    FOR JSON PATH) as 'GuestInfo'
from proteluser.buch
inner join proteluser.kunden on proteluser.kunden.kdnr = proteluser.buch.kundennr
left join proteluser.kunden as AGENCIA on AGENCIA.kdnr = proteluser.buch.reisenr
left join proteluser.kunden as EMPRESA on EMPRESA.kdnr = proteluser.buch.firmennr
left join proteluser.kunden as GRUPO on GRUPO.kdnr = proteluser.buch.gruppennr
left join proteluser.kunden as OUTROS on OUTROS.kdnr = proteluser.buch.sourcenr
inner join proteluser.zimmer on proteluser.zimmer.zinr = proteluser.buch.zimmernr
inner join proteluser.kat on proteluser.kat.katnr = proteluser.buch.katnr
inner join proteluser.zistat on proteluser.zistat.zinr = proteluser.buch.zimmernr
inner join proteluser.ptypgrp on proteluser.ptypgrp.ptgnr = proteluser.buch.preistypgr
where proteluser.buch.mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID}
  and proteluser.buch.buchstatus = 0
  and proteluser.buch.resstatus not in (3,7)
  and proteluser.buch.datumvon = (select pdate from proteluser.datum where mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID})
  and proteluser.kat.zimmer = 1
FOR JSON PATH
