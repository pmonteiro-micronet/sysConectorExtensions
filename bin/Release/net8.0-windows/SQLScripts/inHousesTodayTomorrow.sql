select 
    cast(proteluser.buch.datumvon as date) as 'DateCI',
    cast(proteluser.buch.datumbis as date) as 'DateCO',
    proteluser.kunden.name1 as 'LastName',
    proteluser.kunden.vorname as 'FirstName',
    case when proteluser.buch.reisenr > 0 then AGENCIA.name1 else '' end as 'Booker',
    case when proteluser.buch.firmennr > 0 then EMPRESA.name1 else '' end as 'Company',
    case when proteluser.buch.gruppennr > 0 then GRUPO.name1 else '' end as 'Group',
    case when proteluser.buch.sourcenr > 0 then OUTROS.name1 else '' end as 'Others',
    proteluser.zimmer.ziname as 'Room',
    proteluser.buch.not1txt as 'Notes'
from proteluser.buch
inner join proteluser.kunden on proteluser.kunden.kdnr = proteluser.buch.kundennr
left join proteluser.kunden as AGENCIA on AGENCIA.kdnr = proteluser.buch.reisenr
left join proteluser.kunden as EMPRESA on EMPRESA.kdnr = proteluser.buch.firmennr
left join proteluser.kunden as GRUPO on GRUPO.kdnr = proteluser.buch.gruppennr
left join proteluser.kunden as OUTROS on OUTROS.kdnr = proteluser.buch.sourcenr
inner join proteluser.zimmer on proteluser.zimmer.zinr = proteluser.buch.zimmernr
inner join proteluser.kat on proteluser.kat.katnr = proteluser.buch.katnr
where proteluser.buch.mpehotel = {STATEMENT_INHOUSES_WEBSERVICE.HotelID}  
  and proteluser.buch.buchstatus = 1 
  and proteluser.buch.resstatus not in (3,7) 
  and proteluser.buch.datumvon < (select pdate from proteluser.datum where proteluser.datum.mpehotel = {STATEMENT_INHOUSES_WEBSERVICE.HotelID}) 
  and proteluser.kat.zimmer = 1
FOR JSON PATH
