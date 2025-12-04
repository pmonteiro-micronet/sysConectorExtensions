SELECT * 
FROM protelamadeos.proteluser.kunden 
WHERE (@CompanyVAT IS NULL OR vatno = @CompanyVAT)
  AND (@CompanyName IS NULL OR name1 LIKE '%' + @CompanyName + '%')
