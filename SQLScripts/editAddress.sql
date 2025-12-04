UPDATE proteluser.kunden
SET
    landkz = @CountryID_CodeNr,
    land = @CountryID_Land,
    strasse = @StreetAddress,
    plz = @ZipCode,
    ort = @City,
WHERE kdnr = @ProfileID;