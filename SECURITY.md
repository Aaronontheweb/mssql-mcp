# Security Policy

## Reporting Security Issues

If you discover a security vulnerability, please email security@example.com instead of using the public issue tracker.

## Security Best Practices

When using this MCP server:

1. **Database User**: Create a dedicated SQL user with minimal permissions
2. **Never use sa/admin accounts** in production
3. **Use Windows Authentication** when possible
4. **Enable encryption** for sensitive data: `MSSQL_ENCRYPT=true`
5. **Restrict permissions** to only necessary tables and operations

## SQL Injection Protection

This server includes built-in protection against SQL injection:
- Table names are validated with strict regex patterns
- All identifiers are properly escaped
- User input is parameterized where possible

## Example: Minimal Permissions

```sql
-- Create a restricted user
CREATE LOGIN mcp_user WITH PASSWORD = 'StrongPassword123!';
CREATE USER mcp_user FOR LOGIN mcp_user;

-- Grant only necessary permissions
GRANT SELECT ON Schema.TableName TO mcp_user;
GRANT INSERT, UPDATE ON Schema.AuditLog TO mcp_user;
```