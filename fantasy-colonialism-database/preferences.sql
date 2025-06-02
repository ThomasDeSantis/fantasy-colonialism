SET SQL_SAFE_UPDATES = 0;

SET GLOBAL log_output = 'FILE';
/*SET GLOBAL general_log_file = "/logging/general.log";*/
SET GLOBAL general_log = 'ON';

SET PERSIST cte_max_recursion_depth = 20000;

SET GLOBAL connect_timeout = 3600;
SET GLOBAL max_execution_time = 3600;
SET GLOBAL interactive_timeout = 3600;
SET GLOBAL wait_timeout = 3600;
SET GLOBAL net_read_timeout = 3600;

SET SESSION wait_timeout = 3600;
SET GLOBAL autocommit=0;