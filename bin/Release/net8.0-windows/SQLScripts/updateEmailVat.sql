Update proteluser.kunden
Set vatno = @EditedVAT, 
    Email = @EditedEmail
Where kdnr = @RegisterID;

Select * from proteluser.kunden Where kdnr = @RegisterID;
