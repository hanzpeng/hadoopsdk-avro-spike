######################################################################

# First start Azure PowerShell, then run this script.

######################################################################

# set the following variables

$subscriptionName = "Windows Azure Internal Consumption"
$clusterName = "hanzhdi"

######################################################################

# You may or need to uncomment the following line to add AzureAccount

    #Add-AzureAccount

######################################################################

function RunHive($queryString)
{
    Use-AzureHDInsightCluster $clusterName
    Write-Host($queryString)
    $hiveJobDefinition = New-AzureHDInsightHiveJobDefinition -Query $queryString 
    $hiveJob = Start-AzureHDInsightJob -Cluster $clusterName -JobDefinition $hiveJobDefinition
    #Wait-AzureHDInsightJob -Job $hiveJob -WaitTimeoutInSeconds 3600
    #Get-AzureHDInsightJobOutput -Cluster $clusterName -JobId $hiveJob.JobId -StandardOutput
}

######################################################################

function Query($queryString)
{
    Use-AzureHDInsightCluster $clusterName
    Write-Host($queryString)
    $response = Invoke-Hive -Query $queryString
    Write-Host $response
}

######################################################################

Select-AzureSubscription $subscriptionName
RunHive("DROP TABLE IF EXISTS avrotest;")
RunHive("CREATE EXTERNAL TABLE avrotest ROW FORMAT SERDE 'org.apache.hadoop.hive.serde2.avro.AvroSerDe' STORED AS INPUTFORMAT 'org.apache.hadoop.hive.ql.io.avro.AvroContainerInputFormat' OUTPUTFORMAT 'org.apache.hadoop.hive.ql.io.avro.AvroContainerOutputFormat' LOCATION 'wasb://hanzhdi@hanzstorage.blob.core.windows.net/aaa/avrotest' TBLPROPERTIES ('avro.schema.url'='wasb://hanzhdi@hanzstorage.blob.core.windows.net/aaa_avrotest.avsc');")
Query("Select * from avrotest;")

######################################################################

