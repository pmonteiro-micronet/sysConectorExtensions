UPDATE proteluser.kunden
SET
  gebdat = @DateOfBirth,
  kunden.gebland = @IDCountryOfBirth,
  kunden.nat = @IDNationality,
  doctype = @IDDoc,
  passnr = @NrDoc,
  docvalid = @ExpDate,
  issued = @Issue
WHERE kdnr = @ProfileID