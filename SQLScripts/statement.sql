declare @kdnr int, @tag nvarchar(20), @mpehotel int, @Description nvarchar(200)
 
set @kdnr = (select top 1 kundennr from leist where buchnr={STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window})
set @tag = '1741174727'
set @mpehotel = 2
set @Description = (select short from lizenz where mpehotel = @mpehotel)
 
select (
	select
		(select
				hotel as 'HotelName',
				@Description as 'Description',
				@tag as 'Tag'
		from lizenz
		where mpehotel = @mpehotel
		FOR JSON PATH) as 'HotelInfo',
		(select
				buchnr as 'ReservationNumber',
				buch.crsnumber as 'BookingNumber',
				cast(globdvon as date) as 'DateCI',
				cast(globdbis as date) as 'DateCO',
				zimmer.ziname as 'RoomNumber',
				case when buch.resuser like 'AvailPro%' then 'Imported' else  buch.resuser end as 'UserName',
				buch.anzerw as 'Adults',
				(buch.anzkin1 + buch.anzkin2 + buch.anzkin3 + buch.anzkin4) as 'Childs'
		from buch
		inner join zimmer on zimmer.zinr=buch.zimmernr
		where buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} FOR JSON PATH) as 'Reservation',
		(Select
				anrede as 'Salution',
				vorname as 'FirstName',
				name1 as 'LastName',
				vatno as 'VatNo',
				land as 'Country',
				strasse as 'Street',
				plz as 'PostalCode',
				ort as 'City',
				case when kunden.sprache=-1 then '' else sprache.name end as 'Lang'
		from kunden
		left join sprache on sprache.nr=kunden.sprache
		where kdnr=@kdnr
		FOR JSON PATH) as 'GuestInfo',
		(select
			X.ID,
			X.Date,
			X.Qty,
			X.Description,
			X.Description2,
			X.UnitPrice,
			X.Total
		from
			(select
				ref as 'ID',
				cast(datum as date) as 'Date',
				leist.anzahl as 'Qty',
				text as 'Description',
				zustext as 'Description2',
				epreis as 'UnitPrice',
				epreis*leist.anzahl as 'Total'
			from leist
			where leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window} and grpref<0
			group by grpref,ref,datum,anzahl,grptext,text,epreis,zustext
			union all
			select
				grpref as 'ID',
				cast(datum as date) as 'Date',
				1 as 'Qty',
				grptext as 'Description',
				grpztext as 'Description2',
				sum(epreis*leist.anzahl) as 'UnitPrice',
				sum(epreis*leist.anzahl) as 'Total'
			from leist
			where leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window} and grpref>0
			group by grpref,datum,grptext,grpztext) as X
		FOR JSON PATH) as 'Items',
		(select
				vatno as 'ID',
				cast(mwstsatz as decimal(10,2)) as 'Taxes',
				sum(epreis*leist.anzahl) as 'TotalWithTaxes',
				cast(sum(epreis*leist.anzahl)/(1+(mwstsatz/100)) as decimal(10,2)) as 'TotalWithOutTaxes',
				sum(epreis*leist.anzahl) - sum(epreis*leist.anzahl)/(1+(mwstsatz/100)) as 'TotalTaxes'
			from leist
			where leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window}
			group by mwstsatz,vatno
		FOR JSON PATH) as 'Taxes',
		(select
				sum(epreis*leist.anzahl) as 'Total',
				sum(epreis*leist.anzahl) as 'Balance',
				0 as 'Payment'
			from leist
			where leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window}
		FOR JSON PATH) as 'DocumentTotals'
FOR JSON PATH) as 'Result'