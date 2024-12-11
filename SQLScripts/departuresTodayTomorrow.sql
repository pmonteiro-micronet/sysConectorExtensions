select 
    cast(datumbis as date) as 'DateCO',
    kunden.name1 as 'LastName',
    kunden.vorname as 'FirstName',
    case when buch.reisenr > 0 then AGENCIA.name1 else '' end as 'Booker',
    case when buch.firmennr > 0 then EMPRESA.name1 else '' end as 'Company',
    case when buch.gruppennr > 0 then GRUPO.name1 else '' end as 'Group',
    case when buch.sourcenr > 0 then OUTROS.name1 else '' end as 'Others',
    zimmer.ziname as 'Room',
    buch.not1txt as 'Notes',
    buch.buchnr as 'ResNo'
from proteluser.buch
inner join proteluser.kunden on kunden.kdnr = buch.kundennr
left join proteluser.kunden as AGENCIA on AGENCIA.kdnr = buch.reisenr
left join proteluser.kunden as EMPRESA on EMPRESA.kdnr = buch.firmennr
left join proteluser.kunden as GRUPO on GRUPO.kdnr = buch.gruppennr
left join proteluser.kunden as OUTROS on OUTROS.kdnr = buch.sourcenr
inner join proteluser.zimmer on zimmer.zinr = buch.zimmernr
inner join proteluser.kat on kat.katnr = buch.katnr
where buch.mpehotel = {STATEMENT_CHECKOUTS_WEBSERVICE.HotelID} 
  and buchstatus = 1 
  and resstatus not in (3,7) 
  and datumbis = (select pdate from proteluser.datum where mpehotel = {STATEMENT_CHECKOUTS_WEBSERVICE.HotelID}) 
  and kat.zimmer = 1
union all
select 
    cast(datumbis as date) as 'DateCO',
    kunden.name1 as 'LastName',
    kunden.vorname as 'FirstName',
    case when buch.reisenr > 0 then AGENCIA.name1 else '' end as 'Booker',
    case when buch.firmennr > 0 then EMPRESA.name1 else '' end as 'Company',
    case when buch.gruppennr > 0 then GRUPO.name1 else '' end as 'Group',
    case when buch.sourcenr > 0 then OUTROS.name1 else '' end as 'Others',
    zimmer.ziname as 'Room',
    buch.not1txt as 'Notes',
    buch.buchnr as 'ResNo'
from proteluser.buch
inner join proteluser.kunden on kunden.kdnr = buch.kundennr
left join proteluser.kunden as AGENCIA on AGENCIA.kdnr = buch.reisenr
left join proteluser.kunden as EMPRESA on EMPRESA.kdnr = buch.firmennr
left join proteluser.kunden as GRUPO on GRUPO.kdnr = buch.gruppennr
left join proteluser.kunden as OUTROS on OUTROS.kdnr = buch.sourcenr
inner join proteluser.zimmer on zimmer.zinr = buch.zimmernr
inner join proteluser.kat on kat.katnr = buch.katnr
where buch.mpehotel = {STATEMENT_CHECKOUTS_WEBSERVICE.HotelID} 
  and resstatus not in (3,7) 
  and datumbis = dateadd(day, 1, (select pdate from proteluser.datum where mpehotel = {STATEMENT_CHECKOUTS_WEBSERVICE.HotelID})) 
  and kat.zimmer = 1
FOR JSON PATH
