## Release 0.7.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FON001 | FON | Error | A [FonRecord] type or a type containing it is not partial
FON002 | FON | Error | A member has a type that FON cannot store
FON003 | FON | Error | The type has no constructor the generated reader can use
FON004 | FON | Error | The type cannot be a FON record (generic, abstract, static or ref struct)
FON005 | FON | Error | An accessor of a mapped member is not accessible from the type
FON006 | FON | Error | Two mapped properties have the same name
