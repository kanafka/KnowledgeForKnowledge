# Database Migrations

To create a new migration:
```
dotnet ef migrations add MigrationName --project Infrastructure --startup-project API
```

To apply migrations:
```
dotnet ef database update --project Infrastructure --startup-project API
```

To remove the last migration:
```
dotnet ef migrations remove --project Infrastructure --startup-project API
```


