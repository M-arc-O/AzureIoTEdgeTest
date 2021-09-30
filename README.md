# Introduction 
This repository contains the modules needed to to turn a IoT edge device into a ship shape ship. This readme is focused on using a raspberry pi 2b for IoT egde hardware but the modules can run on any hardware. The modules are build in Visual Studio 2019 (VS).

# Hardware
Raspberry Pi 2B
Arduino Uno
SparkFunMPL3115A2
SparkFunLSM9DS1

The Sprakfun sensor boards are connected to the Arduino by I2C interface the Arduino is connected to the raspberry pi by serial port. The software in the SensorController.ino file gets the values from the sensor and writes them to the serial port.

# Getting Started
Follow the next steps to be able to run the modules on your device.
1.	These steps take care of installing IoT edge on your raspberry pi:
    Follow this guide to install Stretch on the Raspberry Pi:
    https://howchoo.com/pi/how-to-install-raspbian-stretch-on-the-raspberry-pi 

    Create a IoT hub in Azure and add an IoT Edge device, use this device in the next tutorial.

    Follow this guide to install IoT Edge on the Raspberry Pi:
    https://docs.microsoft.com/nl-nl/azure/iot-edge/how-to-install-iot-edge?view=iotedge-2020-11

    Install influxdb client by using this command:
    sudo apt install influxdb-client

2.	Run the following commands on the IoT Edge device to be able to use the serial port, influxdb and grafana:
    # For the serial port:
    sudo chmod 666 /dev/ttyAMA0
    
    Save the following content in a file /etc/rc.local:
    #!/bin/bash
    sudo chmod 666 /dev/ttyS0
    exit 0

    sudo chmod 777 /etc/rc.local

    # For influxdb:
    sudo mkdir /var/influxdb
    sudo chmod 777 /var/influxdb

    Open the influxdb client installed previously and run the following commands:
    CREATE DATABASE "sensordata" WITH DURATION 24h REPLICATION 1 NAME "sensordatarplcy"
    exit

    # For Grafana:
    sudo mkdir /var/grafana
    sudo chmod 777 /var/grafana

3.  Install the Azure IoT Edge Tools for VS in the Manage Extensions view.

# Build and Publish
To be able to build and publish the modules there are a few steps you need to take.
1.  Create a Container regestry in the Azure portal, go to the Access keys tab and enable admin user.
    Copy the username and password and replace the <YourInfoHere> in the deployment.template.json and .env file located in the ShipShapeShipeModules project. The .env file might be hidden.

2.  Change the IP address to the IP address of your device in the program.cs file of the ShipShapeShipInfluxWriter project (function .WriteTo.InfluxDB).

3.  Build and publish the modules by right clicking the ShipShapeShipeModules and pushing the "Build and publish IoTEdge modules" button.

4.  Go to the Cloud Explorer tab in VS, select your edge device in the IoTHub you created and right click to be able to push the "Create deployment" button. Select the deployment.arm32v7.json file in the ShipShapeShipeModules project config folder.

# Test and debug
To test if the modules are installed and running connect to the device using ssh.
The command sudo iotedge list will show you the modules running on the edge devices. Installing modules might take some time and show it might take some time for all modules to show.
To debug the modules you can use the log files on the IoT Edge device, the sudo iotedge system logs shows the system logs form the IoT Edge device. The sudo iotedge logs <module name> shows the logs of a specific module.

To test if data is entered in the data base use the influxdb client with the following commands, there should be multiple measurements:
use senserdata
show measurements

If there are measurements in the influxdb you can show them in the dashboards of Grafana, use the name 'admin' and the password form the deployment.template.json file to login. 

# Sources used to make this solution
https://howchoo.com/pi/how-to-install-raspbian-stretch-on-the-raspberry-pi 
https://docs.microsoft.com/nl-nl/azure/iot-edge/how-to-install-iot-edge?view=iotedge-2020-11 
https://github.com/iot-edge-foundation/iot-edge-serial/tree/fa9af3bd5e4cad7be58fe10c49e06a1084a2948f
https://sandervandevelde.wordpress.com/2021/02/10/using-influx-database-and-grafana-on-the-azure-iot-edge/