#include <Wire.h>
#include "SparkFunMPL3115A2.h"
#include <SparkFunLSM9DS1.h>

#define PRINT_SPEED 1000
static unsigned long lastPrint = 0;

MPL3115A2 myPressure;
LSM9DS1 imu;

short TemperatureType = 0;
short BarometerType = 1;
short GyroscopeType = 2;
short AccelerometerType = 3;
short MagnetometerType = 4;

void printTemperature();
void printPressure();
void printGyroscope();
void printAccelerometer();
void printMagnetometer();

void setup() {
  Serial.begin(9600);

  Wire.begin();

  if (imu.begin() == false) // with no arguments, this uses default addresses (AG:0x6B, M:0x1E) and i2c port (Wire).
  {
    Serial.println("Failed to communicate with LSM9DS1.");
    Serial.println("Double-check wiring.");
    Serial.println("Default settings in this sketch will " \
                   "work for an out of the box LSM9DS1 " \
                   "Breakout, but may need to be modified " \
                   "if the board jumpers are.");
    while (1);
  }

  myPressure.begin(); 
  myPressure.setModeBarometer(); 
  myPressure.setOversampleRate(7); // Set Oversample to the recommended 128
  myPressure.enableEventFlags(); // Enable all three pressure and temp event flags 
}



void loop() {  
  if (imu.gyroAvailable()) { imu.readGyro(); }
  if (imu.accelAvailable()) { imu.readAccel(); }
  if (imu.magAvailable()) { imu.readMag(); }

  if ((lastPrint + PRINT_SPEED) < millis())
  { 
    printTemperature();
    printPressure();
    printGyroscope();  // Print "G: gx, gy, gz"
    printAccelerometer(); // Print "A: ax, ay, az"
    printMagnetometer();   // Print "M: mx, my, mz"
    
    lastPrint = millis(); // Update lastPrint time
  }
}

void printTemperature()
{
  float temperature = myPressure.readTemp();
  Serial.print(TemperatureType); 
  Serial.print(":");
  Serial.print(temperature, 2);    
  Serial.println();
}

void printPressure()
{
  float pressure = myPressure.readPressure();
  Serial.print(BarometerType); 
  Serial.print(":");
  Serial.print(pressure, 2);    
  Serial.println();  
}

void printGyroscope()
{
  Serial.print(GyroscopeType);
  Serial.print(":");
  Serial.print(imu.calcGyro(imu.gx), 2);
  Serial.print(":");
  Serial.print(imu.calcGyro(imu.gy), 2);
  Serial.print(":");
  Serial.print(imu.calcGyro(imu.gz), 2);
  Serial.println();
}

void printAccelerometer()
{
  Serial.print(AccelerometerType);
  Serial.print(":");
  Serial.print(imu.calcAccel(imu.ax), 2);
  Serial.print(":");
  Serial.print(imu.calcAccel(imu.ay), 2);
  Serial.print(":");
  Serial.print(imu.calcAccel(imu.az), 2);
  Serial.println();
}

void printMagnetometer()
{
  Serial.print(MagnetometerType);
  Serial.print(":");
  Serial.print(imu.calcMag(imu.mx), 2);
  Serial.print(":");
  Serial.print(imu.calcMag(imu.my), 2);
  Serial.print(":");
  Serial.print(imu.calcMag(imu.mz), 2);
  Serial.println();
}
