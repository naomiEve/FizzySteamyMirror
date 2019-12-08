# FizzySteamyMirror

This project is a fork of FizzySteamyMirror that utilizes garry's Facepunch.Steamworks library instead of Steamworks.NET. 

## Dependencies

Both of these projects need to be installed and working before you can use this transport.
1. [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks) This fork relies on Facepunch's Steamworks wrapper to communicate with the [Steamworks API](https://partner.steamgames.com/doc/sdk). **Requires .Net 4.x**  
2. [Mirror](https://github.com/vis2k/Mirror) FizzySteamyMirror is also obviously dependant on Mirror which is a streamline, bug fixed, maintained version of UNET for Unity. **Recommended [Stable Version](https://assetstore.unity.com/packages/tools/network/mirror-129321)**

## Setting Up

1. Download and install the dependencies **Download the unitypackage from release for easy all in one**
2. Download **"FizzySteamyMirror"** and place in your Assets folder somewhere. **If errors occur, open a [Issue ticket.](https://github.com/naomiEve/FizzySteamyMirror/issues)**
3. In your NetworkManager object replace Telepathy (or any other active transport) with FizzySteamyMirror.
4. Set the appropriate Steam AppID for your game inside of the FizzySteamyMirror script. (You can use the 480 appid for testing).

## Building

1. When running the game make sure you have placed it into steam as a **Non-Steam Game**
**Note: This is not reuired, but some have reported their steam SDK not working without doing this**

**Note: The 480(Spacewar) appid is a very grey area, technically, it's not allowed but they don't really do anything about it.
If you know a better way around this please make an [Issue ticket.](https://github.com/naomiEve/FizzySteamyMirror/issues)**

**Note: When you have your own appid from steam then replace the 480 with your own game appid.**

## Host

1. Open your game through Steam
2. Host your game through the NetworkManagerHUD
3. If you're playing **Spacewar**, then congratulations, it's working!

**Note: You can run it in Unity aswell**

## Client

1. Send the game to your buddy.
2. Send your Steam64ID to your friend to put in the address box and then click on **Lan Client**.
3. If the client connected, it works!

**Joining through code is the same like with any other transport in Mirror, just pass the steam64id as the address instead.**

## Playtesting your game locally

1. You need to have both **"FizzySteamyMirror"** and **"Telepathy Transport"**
2. To test it locally disable **"FizzySteamyMirror"** and enable **"Telepathy Transport"**
3. To again turn on Steam's P2P transport, enable **"FizzySteamyMirror"** and disable **"Telepathy Transport"**

