SET SQL_SAFE_UPDATES = 0;

SET GLOBAL log_output = 'FILE';
SET GLOBAL general_log_file = "/logging/general.log";
SET GLOBAL general_log = 'ON';

SET PERSIST cte_max_recursion_depth = 20000