select
               Buchnr as 'ResNo',
               globdvon as 'Arrival',
               globdbis as 'Departure',
               K.vorname as 'Lastname',
               isnull(Z.ziname,'') as 'Room',
               K2.kat as 'Room Type',
               P.ptyp as 'Rate Code',
               'EUR' as 'Currency',
               case when exists (select * from varbuch where buchnr = B.leistacc) then (select case when datediff(day,globdvon,globdbis) = 0 then sum(preis) else sum(preis)/datediff(day,globdvon,globdbis) end from varbuch where buchnr = B.leistacc) else case when datediff(day,globdvon,globdbis) = 0 then sum(preis) else B.preis/datediff(day,globdvon,globdbis) end end as 'Price',
               case when B.buchstatus = 0 then R.resbez when B.buchstatus = 1 then 'Checked-in' else 'Checked-out' end as 'Status',
               'Nota 1: ' + B.not1txt + ' ;Nota 2: ' + B.not2txt as 'Notes',
               B.crsnumber as 'CRSNumber',
B.resuser as 'User'
from protelsevero.proteluser.buch as B
inner join protelsevero.proteluser.kunden as K on K.kdnr = B.kundennr
left join protelsevero.proteluser.zimmer as Z on Z.zinr = B.zimmernr
inner join protelsevero.proteluser.resstat as R on R.resnr = B.resstatus
inner join protelsevero.proteluser.kat as K2 on K2.katnr = B.katnr
inner join protelsevero.proteluser.ptyp as P on P.ptypnr = B.preistyp
where B.mpehotel = {MPE_HOTEL} and globdvon between '{START_DATE}' and '{END_DATE}'
group by B.buchnr,B.globdvon,B.globdbis,K.vorname,Z.ziname,K2.kat,P.ptyp,B.not1txt,B.not2txt,B.crsnumber,B.leistacc,B.preis,B.buchstatus,R.resbez,B.resuser
order by globdvon