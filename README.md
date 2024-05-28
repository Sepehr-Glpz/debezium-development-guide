# Setup Debezium

> **Note: This guide is meant for Development purposes only! refer to your database and operation departments for suitable best practices in production environments**

# Foreword

For information on the usage of _debezium_ and _kafka connect_ or _CDC_ please refer to the following links

- [Debezium](https://debezium.io/)
- [Kafka Connect](https://www.confluent.io/blog/fully-managed-connectors-make-apache-kafka-easier/?utm_medium=sem&utm_source=google&utm_campaign=ch.sem_br.nonbrand_tp.prs_tgt.kafka-connectors_mt.xct_rgn.emea_lng.eng_dv.all_con.kafka-connect&utm_term=what+is+kafka+connect&placement=&device=c&creative=&gclid=Cj0KCQjw3tCyBhDBARIsAEY0XNk5H7GoyRIzI8AW_Dtb5S2lLRlg313NUUDO3hC3pFbCgYizHzYjTAYaApV-EALw_wcB&session_ref=https://www.google.com/)
- [SQL Server CDC](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-data-capture-sql-server?view=sql-server-ver16)

---

# Quick Start

You can deploy all these services locally using the provided `docker-compose-quickstart.yaml` file.

simply run

```bash
docker-compose -f docker-compose-quickstart.yaml up
```

> Note: Please study the default compose file prior to deployment as you will need to create custom volumes yourself on your host for the services

- Remarks
  The compose will launch many containers. please make sure your system has sufficient resources: at least 10 GB of RAM dedicated to your docker engine.

run this to remove all containers

```bash
docker-compose -f docker-compose-quickstart.yaml down
```

---

# Required Components

to deploy the debezium architecture we require multiple different components to perform different roles in our system

- A data-source (ie: SqlServer)
- A functioning Apache Kafka Service (We will use KRaft but Zookeeper should no pose any challenges)
- A Debezium Connect Service (Please note that there are breaking changes from v1.x to v2.x)
- A Schema Registry Service (Used later for Avro schema management)
- Some Web based UI panels for these services (We will not configure any auth on any of these services for this guide)

# The Components

## Database

in the compose file we have an massql-server configuration as follows

```yaml
db:
  image: mssql/server:2022-latest
  container_name: sql-db
  hostname: sql-db
  environment:
    ACCEPT_EULA: "Y"
    MSSQL_SA_PASSWORD: ${DB_SA_PASSWORD}
    MSSQL_PID: "Developer"
    TZ: "Asia/Tehran"
    MSSQL_AGENT_ENABLED: "True"
    MSSQL_COLLATION: "Persian_100_CI_AI_SC"
    MSMQL_TRUST_SERVER_CERTIFICATE: "true"
  ports:
    - ${DB_MAP_PORT}:1433
  volumes:
    - F:\data\debkaf\db:/var/opt/mssql/data
  mem_limit: ${DB_MEM_LIMIT}
```

- Important Remarks!
  make sure to replace the `volumes` section with your own custom volume to persist the databas e data across restarts

with its environments in the `.env` file respectively

```bash
# DB variables
DB_MAP_PORT=1433
DB_SA_PASSWORD="@SA@123456789"
DB_MEM_LIMIT="4GB"
```

- Remarks
  The _DB_SA_PASSWORD_ variable will need to be a strong password or the container will fail!
  Prefer setting the memory limit to a value higher than 3GB as any lower might cause the server to fail.

this will launch an sql server container, you can access the server with the tool of your choosing, once connected to the server we will require a test database to use, for our exercise we will create a database named **Catland** and create a **Cats** table. we also need to activate the **CDC** feature on this database and the tables we are going to be using. this script will create the database and table and activate CDC for both. to read more on how debezium uses CDC to track changes read

- [Debezium-SqlServer-Connector](https://debezium.io/documentation/reference/stable/connectors/sqlserver.html)

> Note: you will need a user with `sysadmin` permissions to run these queries. you also need to configure a user as the _db_owner_ with access to the **cdc** schema for later.

run the `db.sql` query on the server

```sql
USE master
GO

CREATE DATABASE Catland
GO

USE Catland
GO

EXEC sys.sp_cdc_enable_db
GO

CREATE TABLE dbo.Cats (
	[Id] BIGINT PRIMARY KEY IDENTITY(1, 1)
	,[Name] VARCHAR(30) NOT NULL
	,[LastName] VARCHAR(30) NULL
	,[Age] INT NULL
	,[CreationDate] DATETIME2(3) NOT NULL
	)
GO

EXEC sys.sp_cdc_enable_table @source_schema = N'dbo'
	,@source_name = N'Cats'
	,@role_name = NULL
	,@supports_net_changes = 1
GO

USE master
GO

SELECT name
	,is_cdc_enabled
FROM sys.databases
WHERE name = 'Catland'
GO
```

we are now done with our database and can move on to the next component.

### Kafka

the compose file contains spec to launch 3 kafka nodes in a _KRaft_ cluster without zookeeper. with a web based UI panel to work with.

> Note: The kafka deployment here will not matter as it wont make much difference for the debezium service.

```yaml
broker-1:
    image: confluentinc/cp-kafka:latest
    container_name: limgrave
    hostname: limgrave
    user: root
    ports:
      - ${KAFKA_NODE_1_MAP_PORT}:9092
    environment:
      KAFKA_NODE_ID: 1
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:PLAINTEXT,INTERNAL:PLAINTEXT,EXTERNAL:PLAINTEXT
      KAFKA_LISTENERS: INTERNAL://limgrave:29092,CONTROLLER://limgrave:9093,EXTERNAL://0.0.0.0:9092
      KAFKA_ADVERTISED_LISTENERS: "INTERNAL://limgrave:29092,EXTERNAL://127.0.0.1:${KAFKA_NODE_1_MAP_PORT}"
      KAFKA_CONTROLLER_LISTENER_NAMES: "CONTROLLER"
      KAFKA_INTER_BROKER_LISTENER_NAME: "INTERNAL"
      KAFKA_CONTROLLER_QUORUM_VOTERS: "1@limgrave:9093,2@liurnia:9093,3@caelid:9093"
      KAFKA_PROCESS_ROLES: "broker,controller"
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: true
      KAFKA_LOG_RETENTION_HOURS: 24
      CLUSTER_ID: ${KAFKA_CLUSTER_ID}
    volumes:
      - F:\data\debkaf\limgrave\data:/var/lib/kafka/data
      - F:\data\debkaf\limgrave\secrets:/etc/kafka/secrets

  broker-2:
    image: confluentinc/cp-kafka:latest
    container_name: liurnia
    hostname: liurnia
    user: root
    ports:
      - ${KAFKA_NODE_2_MAP_PORT}:9092
    environment:
      KAFKA_NODE_ID: 2
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:PLAINTEXT,INTERNAL:PLAINTEXT,EXTERNAL:PLAINTEXT
      KAFKA_LISTENERS: INTERNAL://liurnia:29092,CONTROLLER://liurnia:9093,EXTERNAL://0.0.0.0:9092
      KAFKA_ADVERTISED_LISTENERS: "INTERNAL://liurnia:29092,EXTERNAL://127.0.0.1::${KAFKA_NODE_2_MAP_PORT}"
      KAFKA_CONTROLLER_LISTENER_NAMES: "CONTROLLER"
      KAFKA_INTER_BROKER_LISTENER_NAME: "INTERNAL"
      KAFKA_CONTROLLER_QUORUM_VOTERS: "1@limgrave:9093,2@liurnia:9093,3@caelid:9093"
      KAFKA_PROCESS_ROLES: "broker,controller"
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: true
      KAFKA_LOG_RETENTION_HOURS: 24
      CLUSTER_ID: ${KAFKA_CLUSTER_ID}
    volumes:
      - F:\data\debkaf\liurnia\data:/var/lib/kafka/data
      - F:\data\debkaf\liurnia\secrets:/etc/kafka/secrets

  broker-3:
    image: confluentinc/cp-kafka:latest
    container_name: caelid
    hostname: caelid
    user: root
    ports:
      - ${KAFKA_NODE_3_MAP_PORT}:9092
    environment:
      KAFKA_NODE_ID: 3
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:PLAINTEXT,INTERNAL:PLAINTEXT,EXTERNAL:PLAINTEXT
      KAFKA_LISTENERS: INTERNAL://caelid:29092,CONTROLLER://caelid:9093,EXTERNAL://0.0.0.0:9092
      KAFKA_ADVERTISED_LISTENERS: "INTERNAL://caelid:29092,EXTERNAL://127.0.0.1::${KAFKA_NODE_3_MAP_PORT}"
      KAFKA_CONTROLLER_LISTENER_NAMES: "CONTROLLER"
      KAFKA_INTER_BROKER_LISTENER_NAME: "INTERNAL"
      KAFKA_CONTROLLER_QUORUM_VOTERS: "1@limgrave:9093,2@liurnia:9093,3@caelid:9093"
      KAFKA_PROCESS_ROLES: "broker,controller"
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 3
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: true
      KAFKA_LOG_RETENTION_HOURS: 24
      CLUSTER_ID: ${KAFKA_CLUSTER_ID}
    volumes:
      - F:\data\debkaf\caelid\data:/var/lib/kafka/data
      - F:\data\debkaf\caelid\secrets:/etc/kafka/secrets

  ui:
    image: provectuslabs/kafka-ui
    container_name: erdtree
    hostname: erdtree
    ports:
      - ${KAFKA_UI_MAP_PORT}:8080
    environment:
      KAFKA_CLUSTERS_0_NAME: ${KAFKA_UI_CLUSTER_NAME}
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: "limgrave:29092,liurnia:29092,caelid:29092"
      KAFKA_CLUSTERS_0_SCHEMAREGISTRY: http://regis:8081
      DYNAMIC_CONFIG_ENABLED: "true"
    depends_on:
      - broker-1
      - broker-2
      - broker-3
      - registry
```

- Remarks
  - Do not easily replace the container names or the hostnames as that will mess up the internal networking. when defining connectors it is impoetant to diffrentiate between internal and external networks in docker compose. within the internal network the service address will follow this pattern: **<container_name>:<real_port>.** example: caelid:29092 is the internal network address that will be used by other services like debezium however your hosts application will need to use **localhost:<mapped_port>** to access the cluster.
  - You need to set the _KAFKA_AUTO_CREATE_TOPICS_ENABLE_ to _true_ or debezium will face problems.
  - Also change the volume definitions to suit your host.

with the `.env` file values respectively

```bash
# Kafka variables
KAFKA_NODE_1_MAP_PORT=9092
KAFKA_NODE_2_MAP_PORT=9093
KAFKA_NODE_3_MAP_PORT=9094
KAFKA_CLUSTER_ID="NqnEdODVKkiLTfJvqd1uqQ=="
KAFKA_UI_CLUSTER_NAME="debezium-kafka"
KAFKA_UI_MAP_PORT=8585
```

- Remarks
  - The cluster Id needs to be a base64 encoded string
  - We will not setup any SASL auth mechanism as we will only use this configuration for development

You can visit the UI mapped port on the host local network to see the cluster status.

### Schema Registry

We will also need to setup a schema registry to be used by the other services when utilizing _avro_ serialization. we will use the confluent schemas registry and deploy a UI for it using the following config, you can also see the registry through the kafka UI web panel that we configured in the previous step

```yaml
registry:
  image: confluentinc/cp-schema-registry:latest
  container_name: regis
  hostname: regis
  ports:
    - ${SCHEMA_REG_MAP_PORT}:8081
  environment:
    SCHEMA_REGISTRY_KAFKASTORE_BOOTSTRAP_SERVERS: "limgrave:29092,liurnia:29092,caelid:29092"
    SCHEMA_REGISTRY_HOST_NAME: regis
    SCHEMA_REGISTRY_GROUP_ID: "schema-registry"
    SCHEMA_REGISTRY_ACCESS_CONTROL_ALLOW_METHODS: GET,POST,PUT,OPTIONS
    SCHEMA_REGISTRY_ACCESS_CONTROL_ALLOW_ORIGIN: "*"
  depends_on:
    - broker-1
    - broker-2
    - broker-3

registry-ui:
  image: landoop/schema-registry-ui:latest
  container_name: regis-ui
  hostname: regis-ui
  depends_on:
    - registry
  ports:
    - ${SCHEMA_REG_UI_MAP_PORT}:8000
  environment:
    SCHEMAREGISTRY_URL: "http://regis:8081"
    ALLOW_GLOBAL: 1
    ALLOW_DELETION: 1
    PROXY: "true"
```

With the env variables as

```bash
# Schema Registry variables
SCHEMA_REG_MAP_PORT=7575
SCHEMA_REG_UI_MAP_PORT=7576
```

you can now check the registry from either the UI or the kafka UI service

### Debezium

We have a debezium and a debezium UI service definition as follows within the compose file

```yaml
debra:
  image: debezium/connect:latest
  container_name: debez
  hostname: debez
  ports:
    - ${DEBEZ_MAP_PORT}:8083
  volumes:
    - F:\data\debkaf\debez\kafka\config:/kafka/config
    - F:\data\debkaf\debez\kafka\data:/kafka/data
    - F:\data\debkaf\debez\kafka\logs:/kafka/logs
    - .\libs:/kafka/connect/libs:ro
  environment:
    BOOTSTRAP_SERVERS: "limgrave:29092,liurnia:29092,caelid:29092"
    GROUP_ID: ${DEBEZ_GROUP_ID}
    CONFIG_STORAGE_TOPIC: ${DEBEZ_CONFIG_STORAGE_TOPIC}
    OFFSET_STORAGE_TOPIC: ${DEBEZ_CONFIG_STORAGE_TOPIC}
    STATUS_STORAGE_TOPIC: ${DEBEZ_CONFIG_STORAGE_TOPIC}
    KAFKA_CONNECT_PLUGINS_DIR: "/kafka/connect/"
    KEY_CONVERTER: io.confluent.connect.avro.AvroConverter
    VALUE_CONVERTER: io.confluent.connect.avro.AvroConverter
    CONNECT_KEY_CONVERTER_SCHEMA_REGISTRY_URL: http://regis:8081
    CONNECT_VALUE_CONVERTER_SCHEMA_REGISTRY_URL: http://regis:8081
    CONNECT_TOPIC_CREATION_ENABLE: "true"
    CONNECT_OFFSET_STORAGE_PARTITIONS: 1
    CONNECT_CONFIG_STORAGE_PARTITIONS: 1
    CONNECT_STATUS_STORAGE_PARTITIONS: 1
  depends_on:
    - broker-1
    - broker-2
    - broker-3
    - registry

debra-ui:
  image: debezium/debezium-ui:latest
  container_name: debez-ui
  hostname: debez-ui
  ports:
    - ${DEBEZ_UI_MAP_PORT}:8080
  environment:
    KAFKA_CONNECT_URIS: http://debez:8083
  depends_on:
    - debra
```

- Remarks
  - The volume `.\libs` is an important volume that must exist. it contains the required files for the debezium-confluent-schema-registry plugin that enable debezium to use avro when writing messages to kafka. debezium removed builtin support for avro and confluent schema registry since version 2.x. Read more [HERE](https://debezium.io/documentation/reference/stable/configuration/avro.html#confluent-schema-registry).
  - debezium keeps its required data stored on the kafka service hence the 3 different topic names in the configuration.
  - you must enable kafka connect’s auto topic creation for debeziums ui to function properly.
  - the _VALUE_CONVERTER_ and _KEY_CONVERTER_ keys are set to use avro but the avro and schema registry plugin files will need to be loaded properly for this to function.

> Note: You can download the latest version of the kafka connect avro converter plugin [HERE](https://www.confluent.io/hub/confluentinc/kafka-connect-avro-converter).

```bash
# Debezium variables
DEBEZ_MAP_PORT=9595
DEBEZ_GROUP_ID="my-debezium"
DEBEZ_CONFIG_STORAGE_TOPIC="debez_configs"
DEBEZ_OFFSET_STORAGE_TOPIC="debez_offsets"
DEBEZ_STATUS_STORAGE_TOPIC="debez_status"
DEBEZ_UI_MAP_PORT=9596
```

this will setup debezium and you can visit the UI on your host.

## Defining an SQL Server Connector

Debezium uses a concept called connectors to watch for changes on your SQL server database and publish those changes to kafka. we define these connectors using JSON definitions and sending them to the debezium REST API. the UI can also help us define these configurations using a wizard.

a very basic SQL server connector definition for our example will look like this

```json
{
  "name": "cat-inventory",
  "config": {
    "connector.class": "io.debezium.connector.sqlserver.SqlServerConnector",
    "name": "cat-inventory",
    "topic.prefix": "cat-inv-catland",
    "database.hostname": "sql-db",
    "database.user": "sa",
    "database.password": "@SA@123456789",
    "database.encrypt": "false",
    "database.names": "Catland",
    "tombstones.on.delete": "false",
    "schema.history.internal.kafka.bootstrap.servers": "limgrave:29092,liurnia:29092,caelid:29092",
    "schema.name.adjustment.mode": "avro",
    "schema.history.internal.kafka.topic": "catland-sch-his"
  }
}
```

- Remarks
  - Its important to pass the _database.encrypt_ key as _false_ or the client will fail to connect to our sql server instance.
  - The _database.user_ must be a user with the proper access permissions in the target database for cdc
  - the _schema.history.internal.kafka.topic_ key is required and will be used for the schema change history internally
  - the topic prefix will be used for all topics created for this connector
  - the pattern used for topic creation is **<prefix>.<db_server>.<schema>.<table_name>** so for our example this will create the _cat-inv-catland.Catland.dbo.Cats_ topic for changes made on the _Cats_ table.
  - the _tombstones.on.delete_ key is used for handling DELETE events. for our example we will not be using this feature.

To register this connector use the debezium REST API and use HTTP **POST** to send the JSON body to the `/connectors` route.

> Note: debezium follows the rest spec fairly well. you can delete the connector by sending an HTTP **DELETE** request to `/connectors/<connector_name>`

After sending registering the connector you can see it’s status on the debezium UI web panel. if it has a status of Failed make sure to read the container logs. a healthy connector will have a status of _Running_ with a green color.

Debezium will write all changes it detect for CREATE, UPDATE, DELETE on the table and publish them as messages on the tables corresponding topic on kafka. the messages follow a specific pattern when used with the _AvroConvertor_ plugin and schema registry. the message will contain 2 main fields. the **before** and **after** fields specify how a table row was changed. the payload will also contain a field called **op** which declares the operation; possible values for op are: **”c”** for _Create_, **”u”** for _Update_ and **”d”** for _Delete_. here is an example for an inserted row

```json
{
  "before": null, // before is null for creation operations
  "after": {
    // after is the inserted value
    "Value": {
      "Id": 8,
      "Name": {
        "string": "Sepehr"
      },
      "LastName": {
        "string": "Golpazir"
      },
      "Power": {
        "int": 8585
      },
      "CreationDate": {
        "long": 1716809601923
      }
    }
  },
  "source": {
    // source is extra metadata for the message
    "version": "2.5.0.Final",
    "connector": "sqlserver",
    "name": "catter-catland",
    "ts_ms": 1716793401923,
    "snapshot": {
      "string": "false"
    },
    "db": "Catland",
    "schema": "dbo",
    "table": "Cats",
    "change_lsn": {
      "string": "0000002e:000038a0:0002"
    },
    "commit_lsn": {
      "string": "0000002e:000038a0:0003"
    },
    "event_serial_no": {
      "long": 1
    }
  },
  "op": "c", // the operation -> c: Create
  "ts_ms": {
    "long": 1716793405787
  }
}
```

- Remark
  The **”source”** field contains metadata for the message but we will not go into that in this guide.

**Message Schema**:

When using avro along a schema registry debezium will register the generated schema for the message in the registry with a pattern. the pattern is: _<topic_name>-key_ and _<topic_name>-value._

**Avro Message Payload**:

The message payload that debezium produces when using avro **IS NOT** the above example entirely

The first 5 bytes of the message binary payload will indicate the **schema Id** within the registry followed by a magic byte. this is used in the various kafka avro client drivers to deserialize data using the schema registry. more on this later. read about this more here

- [Debezium Avro Serialization](https://debezium.io/blog/2016/09/19/Serializing-Debezium-events-with-Avro/#avro_serialization)

**Delete Events**

For delete events, debezium by default generates 2 separate messages. the `"op": "d"` event where the `before` field will be the deleted value and the `after` field will be null. **AND** a special message called the _Tombstone_ Event. You can read more about tombstone events [HERE](https://debezium.io/documentation/reference/stable/transformations/event-flattening.html#event-flattening-behavior). we will disable the tombstone event for our example because of complications when deserializing avro messages with different schemas on the same topic using the _"tombstones.on.delete": "false"_ key.

---

## Using The dotnet kafka client with debezium

To use the csharp client you will need to install several nuget packages:

- **Confluent.Kafka**
  This is the normal CSharp kafka client package
- **Confluent.SchemaRegistry**
  This adds support for connecting to the confluent schema registry using the `CachedSchemaRegistryClient` class.
- **Confluent.SchemaRegistry.Serdes.Avro**
  To add Avro support when using the schema registry
- **Chr.Avro.Confluent**
  A community supported package used for setting automatic serializers when working with the confluent kafka client

Here is the working set for a dotnet 6.0 project.

```xml
 <ItemGroup>
    <PackageReference Include="Chr.Avro.Confluent" Version="10.3.0" />
    <PackageReference Include="Confluent.Kafka" Version="2.4.0" />
    <PackageReference Include="Confluent.SchemaRegistry" Version="2.4.0" />
    <PackageReference Include="Confluent.SchemaRegistry.Serdes.Avro" Version="2.4.0" />
  </ItemGroup>
```

> Note: The cached registry client will make requests to the server and fetch its required schemas and caches them locally to prevent further round trips.

First write POCO classes for the messages you are going to consume, make sure to handle changes to the model in the future yourself.

```csharp
// the class we will use for the database record
public class Cat
{
    public long Id { get; set; }

    public int? Power { get; set; }

    public string? Name { get; set; }

    public string? LastName { get; set; }

    public long? CreationDate { get; set; }
}

// the class we will use for the events
public class CatEvent
{
    public Cat? Before { get; set; }

    public Cat? After { get; set; }

    public string? Op { get; set; }
}
```

> IMPORTANT: the avro serializer is extremely picky about the schema and nullability of the fields and types so be careful.

> Note: The library does not natively support `Datetime` types and you will have to use a `long` instead to receive time instances in their UNIX timestamp format.

The create the schema registry client

```csharp
var registryConfig = new SchemaRegistryConfig()
{
    Url = "http://127.0.0.1:7575", // use the registry address here
};

var schemaClient = new CachedSchemaRegistryClient(registryConfig);
```

Next up setup your kafka consumer like you would the normal way and simply add the avro serializer when building the consumer.

```csharp
var consumerConfig = new ConsumerConfig()
{
    // kafka cluster addresses use the host mapped port addresses
    BootstrapServers = "127.0.0.1:9092,127.0.0.1:9093,127.0.0.1:9094",
    GroupId = "my-group", // use a unique consumer group id for each application
    AutoOffsetReset = AutoOffsetReset.Earliest,
};

// we set the key type to the builtin Ignore type to ignore them
// pass the registry client to the builder to set the deserializer
var consumer = new ConsumerBuilder<Ignore, CatEvent>(consumerConfig)
    .SetAvroValueDeserializer(schemaClient) // this is the most important line of code
    .Build();
```

To connect to the debezium kafka topic we will need the name of the topic, in our case this is the `cat-inv-catland.Catland.dbo.Cats` topic.

```csharp
consumer.Subscribe("catter-catland.Catland.dbo.Cats");

var msg = consumer.Consume();
```

Run the `consumer.Consume()` method in a loop and you should start receiving message from the topic.
