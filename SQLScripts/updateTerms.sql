update optin
    set date=GETDATE(),
    user2='MyExtensions',
    method=2,
    status=0
where kdnr={STATEMENT_UPDATETERMS_WEBSERVICE.GuestID}