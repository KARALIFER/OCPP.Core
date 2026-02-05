BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "ChargePoint" (
	"ChargePointId"	TEXT NOT NULL UNIQUE,
	"Name"	TEXT,
	"Comment"	TEXT,
	"Username"	TEXT,
	"Password"	TEXT,
	"ClientCertThumb"	TEXT,
	PRIMARY KEY("ChargePointId")
);
CREATE TABLE IF NOT EXISTS "Users" (
	"UserId"	INTEGER NOT NULL UNIQUE,
	"Username"	TEXT NOT NULL UNIQUE,
	"Password"	TEXT NOT NULL,
	"IsAdmin"	INTEGER NOT NULL,
	"PublicId"	TEXT NOT NULL DEFAULT (lower(hex(randomblob(16)))),
	"CreatedAt"	TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
	"UpdatedAt"	TEXT NOT NULL DEFAULT (CURRENT_TIMESTAMP),
	PRIMARY KEY("UserId" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "ChargeTags" (
	"TagId"	TEXT NOT NULL UNIQUE,
	"TagUid"	TEXT NOT NULL,
	"TagName"	TEXT,
	"ParentTagId"	TEXT,
	"ExpiryDate"	TEXT,
	"Blocked"	INTEGER,
	"UserAccountId"	INTEGER,
	PRIMARY KEY("TagId"),
	FOREIGN KEY("UserAccountId") REFERENCES "Users"("UserId") ON DELETE SET NULL
);
CREATE TABLE IF NOT EXISTS "UserChargePoints" (
	"UserId"	INTEGER NOT NULL,
	"ChargePointId"	TEXT NOT NULL,
	"IsHidden"	INTEGER NOT NULL DEFAULT 0,
	PRIMARY KEY("UserId","ChargePointId"),
	FOREIGN KEY("UserId") REFERENCES "Users"("UserId") ON DELETE CASCADE,
	FOREIGN KEY("ChargePointId") REFERENCES "ChargePoint"("ChargePointId") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "MessageLog" (
	"LogId"	INTEGER NOT NULL UNIQUE,
	"LogTime"	TEXT,
	"ChargePointId"	TEXT,
	"ConnectorId"	INTEGER,
	"Message"	TEXT,
	"Result"	TEXT,
	"ErrorCode"	TEXT,
	PRIMARY KEY("LogId" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "Transactions" (
	"TransactionId"	INTEGER NOT NULL UNIQUE,
	"Uid"	TEXT,
	"ChargePointId"	TEXT,
	"ConnectorId"	INTEGER,
	"StartTagId"	TEXT,
	"StartTime"	TEXT,
	"MeterStart"	REAL,
	"StartResult"	TEXT,
	"StopTagId"	TEXT,
	"StopTime"	TEXT,
	"MeterStop"	REAL,
	"StopReason"	TEXT,
	PRIMARY KEY("TransactionId" AUTOINCREMENT)
	FOREIGN KEY(ChargePointId) REFERENCES ChargePoint(ChargePointId)
);

/**** New with V1.1.0 ****/
CREATE TABLE IF NOT EXISTS "ConnectorStatus" (
	"ChargePointId"	TEXT NOT NULL,
	"ConnectorId"	INTEGER,
	"ConnectorName"	TEXT,
	"LastStatus"	TEXT,
	"LastStatusTime"	TEXT,
	"LastMeter"	TEXT,
	"LastMeterTime"	TEXT,
	PRIMARY KEY("ChargePointId","ConnectorId")
	FOREIGN KEY(ChargePointId) REFERENCES ChargePoint(ChargePointId)
);
CREATE VIEW IF NOT EXISTS "ConnectorStatusView"
AS
SELECT cs.ChargePointId, cs.ConnectorId, cs.ConnectorName, cs.LastStatus, cs.LastStatusTime, cs.LastMeter, cs.LastMeterTime, t.TransactionId, t.StartTagId, t.StartTime, t.MeterStart, t.StartResult, t.StopTagId, t.StopTime, t.MeterStop, t.StopReason
FROM ConnectorStatus AS cs LEFT OUTER JOIN
     Transactions AS t ON t.ChargePointId = cs.ChargePointId AND t.ConnectorId = cs.ConnectorId
WHERE  (t.TransactionId IS NULL) OR
                  (t.TransactionId IN
                      (SELECT MAX(TransactionId) AS Expr1
                       FROM     Transactions
                       GROUP BY ChargePointId, ConnectorId));
/**** New with V1.5.0 ****/
CREATE UNIQUE INDEX IF NOT EXISTS "PK_ChargePointId" ON "ChargePoint" (
	"ChargePointId"	ASC
);
CREATE UNIQUE INDEX IF NOT EXISTS "PK_ChargeTagId" ON "ChargeTags" (
	"TagId"	ASC
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChargeTags_TagUid" ON "ChargeTags" (
	"TagUid"	ASC
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChargeTags_UserAccountId" ON "ChargeTags" (
	"UserAccountId"	ASC
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username" ON "Users" (
	"Username"	ASC
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_PublicId" ON "Users" (
	"PublicId"	ASC
);
CREATE INDEX IF NOT EXISTS "IX_UserChargePoints_ChargePointId" ON "UserChargePoints" (
	"ChargePointId"	ASC
);
CREATE UNIQUE INDEX IF NOT EXISTS "PK_TransactionId" ON "Transactions" (
	"TransactionId"
);
CREATE INDEX IF NOT EXISTS "IX_Transaction_UID" ON "Transactions" (
	"Uid"	ASC
);

COMMIT;
