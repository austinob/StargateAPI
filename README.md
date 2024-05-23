# Stargate Exercise

## Some Notes

1. **Data Model:** Not sure I really understand the need for the AstronautDetail table, which duplicates the person's most recent duty details, which (along with historical duties) are stored in the AstronautDuty table (?)
2. **REST API:** using (potentially) case-sensitive plain-text to identify a resource is not something for the real world (normally would use a DB resource ID or a GUID) - but, totally fine for an exercise :)
3. **Entity vs Dapper:** the CreateAstronautDuty* classes had a mixture - I figured I'd go exclusively with Entity there.
4. **TODO:** some TODO comments have been added in the code.
