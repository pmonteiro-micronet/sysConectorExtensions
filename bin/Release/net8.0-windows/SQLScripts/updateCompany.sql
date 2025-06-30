update proteluser.kunden
set
               name1='<CompanyName_string80>',
               landkz='<CountryID_CodeNrColumnFromNatcode_integer>',
               land='<CountryID_LandColumnFromNatcode_string80>',            
               strasse='<StreetAddress_string80>',
               plz='<ZipCode_string17>',
               ort='<City_string50>',
               region='<state_string80>',
               nat='<vatNO_string30>',
               email='<emailaddress_string75>'
where kdnr='<ProfileID_integer>'