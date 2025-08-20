Co tím získáš
	•	Swagger ukáže nové sekce:
	•	GET /api/v1/samples – přehled dostupných testovacích hodnot + hotové URL pro volání.
	•	GET /api/v1/samples/providers|workers|ps|hdr – kompletní seed data.
	•	GET /api/v1/samples/body/reklamace a /body/notifikace – hotové request body pro POSTy.
	•	Běžné endpointy vrací 200 OK pro vzorová data:
	•	IČO: 12345678, 87654321
	•	KRZP ID: 100001, 100002
	•	RID: 1234567891, 2345678902
	•	Chybové stavy zůstávají zachované při nevalidních parametrech.

Spusť znovu projekt a v Swagger UI otevři skupinu /samples.
