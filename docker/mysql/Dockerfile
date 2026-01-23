FROM mysql:8.0

# Copy custom MySQL configuration
COPY conf/my.cnf /etc/mysql/conf.d/custom.cnf

# Copy initialization scripts (executed in alphabetical order)
COPY init/ /docker-entrypoint-initdb.d/

# Set proper permissions
RUN chmod 644 /etc/mysql/conf.d/custom.cnf

# Expose MySQL port
EXPOSE 3306

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=5 \
    CMD mysqladmin ping -h localhost -u root -p${MYSQL_ROOT_PASSWORD} || exit 1
