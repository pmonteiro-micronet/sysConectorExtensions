declare @ValorTaxaT int, @Hoje date
 
	set @Hoje = (select pdate from datum where mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID} )
	-- temos de alterar o stabnr para o ID que representa a tabela de divisão relativo à taxa turistica
	set @ValorTaxaT = (select betrag from splittab where @Hoje between fromdate and todate and stabnr=22)
 
		select
			(select
					buch.buchnr as 'ResNo',
					cast(datumvon as date) as 'DateCI',
					cast(datumbis as date) as 'DateCO',
					case when buch.reisenr > 0 then AGENCIA.name1 else '' end as 'Booker',
					case when buch.firmennr > 0 then EMPRESA.name1 else '' end as 'Company',
					case when buch.gruppennr > 0 then GRUPO.name1 else '' end as 'Group',
					case when buch.sourcenr > 0 then OUTROS.name1 else '' end as 'Others',
					zimmer.ziname as 'Room',
					buch.not1txt as 'Notes',
					case when zistat.status = 1 then 'Cleaned' when zistat.status = 2 then 'Dirty' when zistat.status = 3 then 'Out Of Service' 
					when zistat.status = 4 then 'Checked' when zistat.status = 5 then 'Touched' when zistat.status = 6 then 'Cleaning in Progress'
					else 'Cleaning Schedule' end as 'RoomStatus',
					kat.kat as 'RoomType',
					buch.anzerw as 'Adults',
					buch.anzkin1+buch.anzkin2+buch.anzkin3+buch.anzkin4 as 'Childs',
					ptypgrp.gruppe as 'RateCode',
					case when exists (select * from varbuch where buchnr=buch.buchnr) then cast((select sum(preis)/DATEDIFF(day,datumvon,datumbis) from varbuch where buchnr=buch.buchnr) as decimal(10,2)) else buch.grundpreis end as 'Price',
					case when DATEDIFF(day,datumvon,datumbis) > 7 then @ValorTaxaT*buch.anzerw*7 else @ValorTaxaT*buch.anzerw*DATEDIFF(day,datumvon,datumbis) end as 'CityTax',
					case when exists (select * from varbuch where buchnr=buch.buchnr) then cast((select sum(preis) from varbuch where buchnr=buch.buchnr) as decimal(10,2)) else buch.grundpreis end as 'Total'
				FOR JSON PATH) as 'ReservationInfo',
				(Select
					(select
						buch.kundennr as 'ProfileID',
						anrede as 'Salution',
						name1 as 'LastName',
						vorname as 'FirstName'
					FOR JSON PATH) as 'GuestDetails',
					(select
						kunden.land as 'Country',
						strasse as 'Street',
						plz as 'PostalCode',
						ort as 'City',
						region as 'Region'
					FOR JSON PATH) as 'Address',
					(Select
						gebdat as 'DateOfBirth',
						natcode.land as 'CountryOfBirth',
						N2.land as 'Nationality',	
						xdoctype.text as 'IDDoc',
						passnr as 'NrDoc',
						docvalid as 'ExpDate',
						issued as 'Issue'
					FOR JSON PATH) as 'PersonalID',
					(select
						email as 'Email',
						telefonnr as 'PhoneNumber',
						vatno as 'VatNo'
					FOR JSON PATH) as 'Contacts'
					from kunden
					left join sprache on sprache.nr=kunden.sprache
					left join xdoctype on xdoctype.ref=kunden.doctype
					left join natcode on natcode.codenr=kunden.gebland
					left join natcode as N2 on N2.codenr=kunden.nat
					where kdnr=buch.kundennr
				FOR JSON PATH) as 'GuestInfo'
			from buch
			inner join kunden on kunden.kdnr=buch.kundennr
			left join kunden as AGENCIA on AGENCIA.kdnr=buch.reisenr
			left join kunden as EMPRESA on EMPRESA.kdnr=buch.firmennr
			left join kunden as GRUPO on GRUPO.kdnr=buch.gruppennr
			left join kunden as OUTROS on OUTROS.kdnr=buch.sourcenr
			inner join zimmer on zimmer.zinr = buch.zimmernr
			inner join kat on kat.katnr=buch.katnr
			inner join zistat on zistat.zinr=buch.zimmernr
			inner join ptypgrp on ptypgrp.ptgnr=buch.preistypgr
			where buch.mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID}   and buchstatus = 0 and buch.resstatus not in (3,7) and datumvon = (select pdate from datum where mpehotel = {STATEMENT_CHECKINS_WEBSERVICE.HotelID}  ) and kat.zimmer=1
		FOR JSON PATH