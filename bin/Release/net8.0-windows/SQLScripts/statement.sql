declare @kdnr int, @tag nvarchar(20), @mpehotel int, @Description nvarchar(200)
 
set @kdnr = (select top 1 kundennr from proteluser.leist where buchnr={STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window})
set @tag = '1741174727'
set @mpehotel = 2
set @Description = (select short from proteluser.lizenz where mpehotel = @mpehotel)
 
select (
	select
		(select
				hotel as 'HotelName',
				@Description as 'Description',
				@tag as 'Tag'
		from proteluser.lizenz
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
		from proteluser.buch
		inner join proteluser.zimmer on proteluser.zimmer.zinr=proteluser.buch.zimmernr
		where buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} FOR JSON PATH) as 'Reservation',
		(select
				anrede as 'Salution',
				vorname as 'FirstName',
				name1 as 'LastName',
				vatno as 'VatNo',
				land as 'Country',
				strasse as 'Street',
				plz as 'PostalCode',
				ort as 'City',
				case when proteluser.kunden.sprache=-1 then '' else sprache.name end as 'Lang'
		from proteluser.kunden
		left join proteluser.sprache on proteluser.sprache.nr=proteluser.kunden.sprache
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
				proteluser.leist.anzahl as 'Qty',
				text as 'Description',
				zustext as 'Description2',
				epreis as 'UnitPrice',
				epreis*proteluser.leist.anzahl as 'Total'
			from proteluser.leist
			where proteluser.leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window} and grpref<0
			group by grpref,ref,datum,anzahl,grptext,text,epreis,zustext
			union all
			select
				grpref as 'ID',
				cast(datum as date) as 'Date',
				1 as 'Qty',
				grptext as 'Description',
				grpztext as 'Description2',
				sum(epreis*proteluser.leist.anzahl) as 'UnitPrice',
				sum(epreis*proteluser.leist.anzahl) as 'Total'
			from proteluser.leist
			where proteluser.leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window} and grpref>0
			group by grpref,datum,grptext,grpztext) as X
		FOR JSON PATH) as 'Items',
		(select
				vatno as 'ID',
				cast(mwstsatz as decimal(10,2)) as 'Taxes',
				sum(epreis*proteluser.leist.anzahl) as 'TotalWithTaxes',
				cast(sum(epreis*proteluser.leist.anzahl)/(1+(mwstsatz/100)) as decimal(10,2)) as 'TotalWithOutTaxes',
				sum(epreis*proteluser.leist.anzahl) - sum(epreis*proteluser.leist.anzahl)/(1+(mwstsatz/100)) as 'TotalTaxes'
			from proteluser.leist
			where proteluser.leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window}
			group by mwstsatz,vatno
		FOR JSON PATH) as 'Taxes',
		(select
				sum(epreis*proteluser.leist.anzahl) as 'Total',
				sum(epreis*proteluser.leist.anzahl) as 'Balance',
				0 as 'Payment'
			from proteluser.leist
			where proteluser.leist.buchnr = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.ResNumber} and rechnung = {STATEMENT_EXTRACTOCONTA_WEBSERVICE.window}
		FOR JSON PATH) as 'DocumentTotals'
FOR JSON PATH) as 'Result'
