# Iot Hub Synchronizer

The goal of the IoT Hub Syncronizer is to mirror IoT device configurations and IoT Edge device configurations from a master Azure IoT Hub to a slave. It can be run as an Azure function or a WebAPI app. It is triggered either via a timer or via EventGrid events for Azure IoT Hub device creation or deletion.

The IoT Hub Synchronizer syncronizes:

- IoT Hub IoT Devices
  - Device Id
  - Primary Key
  - Secondary Key
  - Connection state to IoT Hub (enable/disable)
  - Device Twin
  - IoT Edge parent device

- IoT Edge Devices
  - Device id
  - Primary Key
  - Secondary Key
  - Connextion state to IoT Hub (enable/disable)
  - Device Twin

## Project Structure

- **IotHubSync.AzureFunction**: The Azure Function project
- **IotHubSync.Logic**: The core logic of the IoT Hub Synchronizer
- **IotHubSync.Service**: The WebAPI app
- **IotHubSync.TestConsoleApp**: A DotNet Core console app that can be used to test the functionality of the Synchronizer.

## Setting up the Azure Function

tbd.

## Setting up the WebAPI app

tbd.

## Configuring Azure Event Grid

tbd.

## Missing features

- Synchronization of IoT Edge device Modules and Module Twins
