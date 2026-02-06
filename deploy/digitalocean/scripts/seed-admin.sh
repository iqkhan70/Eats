#!/usr/bin/env bash
# Seed admin role and admin user. Run from /opt/traditionaleats on the server.
# Usage: bash deploy/digitalocean/scripts/seed-admin.sh [admin-email] [admin-password]
# Defaults: admin@traditionaleats.com / Admin123!

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."
source .env 2>/dev/null || true

ADMIN_EMAIL="${1:-admin@traditionaleats.com}"
ADMIN_PASSWORD="${2:-Admin123!}"
MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD:?Set MYSQL_ROOT_PASSWORD in .env}"
DB_NAME="traditional_eats_identity"

echo "=========================================="
echo "Seeding admin user: $ADMIN_EMAIL"
echo "=========================================="

# Wait for MySQL to be ready
echo "Waiting for MySQL to be ready..."
for i in {1..30}; do
  if docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T mysql mysqladmin ping -h localhost -uroot -p"$MYSQL_ROOT_PASSWORD" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

# Try registration API first (creates customer profile too)
echo "Attempting registration via API..."
for i in {1..30}; do
  if docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T identity-service echo "OK" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

if docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T identity-service echo "OK" >/dev/null 2>&1; then
  IDENTITY_PORT=$(docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T identity-service printenv ASPNETCORE_URLS 2>/dev/null | grep -oP ':\K\d+' | head -1 || echo "5000")
  IDENTITY_URL="http://identity-service:$IDENTITY_PORT"
  
  REGISTER_RESPONSE=$(docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T identity-service sh -c "
    curl -s -w '\n%{http_code}' -X POST '$IDENTITY_URL/api/auth/register' \
      -H 'Content-Type: application/json' \
      -d '{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\",\"firstName\":\"Admin\",\"lastName\":\"User\",\"displayName\":\"Admin User\",\"phoneNumber\":\"\",\"role\":\"Admin\"}' 2>/dev/null || echo -e '\n000'
  " || echo -e "\n000")
  
  HTTP_CODE=$(echo "$REGISTER_RESPONSE" | tail -1)
  RESPONSE_BODY=$(echo "$REGISTER_RESPONSE" | head -n -1)
  
  if [ "$HTTP_CODE" = "200" ]; then
    echo "✓ Admin user registered successfully via API!"
    echo ""
    echo "=========================================="
    echo "Admin user seeding complete!"
    echo "Email: $ADMIN_EMAIL"
    echo "Password: $ADMIN_PASSWORD"
    echo "=========================================="
    exit 0
  elif [ "$HTTP_CODE" = "400" ] && echo "$RESPONSE_BODY" | grep -q "already exists"; then
    echo "Admin user already exists. Ensuring Admin role..."
  else
    echo "API registration failed (HTTP $HTTP_CODE: $RESPONSE_BODY). Using direct SQL..."
  fi
fi

# Direct SQL approach (fallback or for role assignment)
echo "Using direct SQL to ensure Admin role and user..."

# Generate password hash using identity-service (it has PasswordHasher)
echo "Generating password hash..."
# Create a temporary C# file to hash the password
PASSWORD_HASH=$(docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T identity-service sh -c '
  cat > /tmp/hash.cs << '\''EOF'\''
using System;
using Microsoft.AspNetCore.Identity;
var ph = new PasswordHasher<object>();
Console.WriteLine(ph.HashPassword(null, Environment.GetEnvironmentVariable("PWD")));
EOF
  export PWD="'"$ADMIN_PASSWORD"'"
  cd /app && dotnet exec TraditionalEats.IdentityService.dll --hash "$PWD" 2>/dev/null || \
  (cd /tmp && dotnet script hash.cs 2>/dev/null) || echo ""
' || echo "")

# If that failed, try using the registration endpoint to get a hash (register then delete, but that's messy)
# Better: use a known working method
if [ -z "$PASSWORD_HASH" ] || [ ${#PASSWORD_HASH} -lt 20 ]; then
  echo "Hash generation failed. Using registration API to create user..."
  # The API approach above should have worked, but if we're here, let's try one more time
  # or provide manual instructions
  echo "Note: Password hash generation requires .NET runtime."
  echo "Please ensure identity-service container has dotnet available."
  echo ""
  echo "Alternative: Register via web UI, then run this script again to assign Admin role."
  PASSWORD_HASH=""
fi

# If hash generation failed, we need to use the API or manual registration
if [ -z "$PASSWORD_HASH" ] || [ "$PASSWORD_HASH" = "" ]; then
  echo "Warning: Could not generate password hash automatically."
  echo ""
  echo "Please register admin manually:"
  echo "  1. Go to http://$(docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T edge hostname -I 2>/dev/null | awk '{print $1}' || echo 'YOUR_SERVER_IP')/register"
  echo "  2. Register with email: $ADMIN_EMAIL, password: $ADMIN_PASSWORD"
  echo "  3. Then run this script again to assign Admin role, or use SQL:"
  echo ""
  echo "     docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T mysql mysql -uroot -p\$MYSQL_ROOT_PASSWORD $DB_NAME << 'SQL'"
  echo "     SET @admin_role_id = (SELECT Id FROM Roles WHERE Name = 'Admin' LIMIT 1);"
  echo "     SET @admin_user_id = (SELECT Id FROM Users WHERE LOWER(Email) = LOWER('$ADMIN_EMAIL') LIMIT 1);"
  echo "     INSERT IGNORE INTO UserRoles (UserId, RoleId) VALUES (@admin_user_id, @admin_role_id);"
  echo "     SQL"
  echo ""
  exit 1
fi

# Insert via SQL
docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" "$DB_NAME" << EOF
-- Ensure Admin role exists
INSERT IGNORE INTO Roles (Id, Name, Description, CreatedAt)
VALUES (UUID(), 'Admin', 'System administrator with full access', UTC_TIMESTAMP());

-- Create admin user if it doesn't exist
INSERT INTO Users (Id, Email, PasswordHash, Status, CreatedAt, FailedLoginAttempts)
SELECT UUID(), LOWER('$ADMIN_EMAIL'), '$PASSWORD_HASH', 'Active', UTC_TIMESTAMP(), 0
WHERE NOT EXISTS (SELECT 1 FROM Users WHERE LOWER(Email) = LOWER('$ADMIN_EMAIL'));

-- Assign Admin role to admin user
INSERT INTO UserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM Users u
CROSS JOIN Roles r
WHERE LOWER(u.Email) = LOWER('$ADMIN_EMAIL')
  AND r.Name = 'Admin'
  AND NOT EXISTS (
    SELECT 1 FROM UserRoles ur 
    WHERE ur.UserId = u.Id AND ur.RoleId = r.Id
  );

SELECT 'Admin user and role ensured' AS Result;
EOF

echo ""
echo "=========================================="
echo "Admin user seeding complete!"
echo "Email: $ADMIN_EMAIL"
echo "Password: $ADMIN_PASSWORD"
echo "=========================================="
echo ""
echo "You can now log in with these credentials."
