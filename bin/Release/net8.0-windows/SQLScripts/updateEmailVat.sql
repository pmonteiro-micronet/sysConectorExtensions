Update proteluser.kunden
Set vatno = @EditedVAT, 
    Email = @EditedEmail
Where kdnr = @RegisterID;