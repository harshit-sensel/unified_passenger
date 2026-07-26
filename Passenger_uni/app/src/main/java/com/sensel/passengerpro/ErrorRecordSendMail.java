package com.sensel.passengerpro;

import android.util.Log;

import org.ksoap2.SoapEnvelope;
import org.ksoap2.serialization.PropertyInfo;
import org.ksoap2.serialization.SoapObject;
import org.ksoap2.serialization.SoapSerializationEnvelope;
import org.ksoap2.transport.HttpTransportSE;

import java.text.SimpleDateFormat;

/**
 * Created by MS on 29-Aug-16.
 */
public class ErrorRecordSendMail {
    private static final String APP_NAME = "Passenger Pro";
    private final String WSDL_TARGET_NAMESPACE = "http://tempuri.org/";
    private String user="AKIAI7RL2GICUI36U4JQ";
    private String pass="Av1Oj3h5creKYz/TrcM8x9TdAA5UnIA1dkHzRKxJlLn4";

    public void errorrecordSendMail(String error) {
        SimpleDateFormat sdf = new SimpleDateFormat("dd/MM/yyyy hh:mm:ss a");
        String datetime=String.valueOf(sdf.format(System.currentTimeMillis()));
        Object response = null;
        String soapaddress = "http://ui.mysensel.com/Services.asmx";
        String SOAP_ACTION = "http://tempuri.org/InsertErrorRecord";
        String OPERATION_NAME = "InsertErrorRecord";
        SoapObject request = new SoapObject(WSDL_TARGET_NAMESPACE, OPERATION_NAME);
        PropertyInfo pi = new PropertyInfo();
        pi.setName("error");
        pi.setValue(error);
        pi.setType(String.class);
        request.addProperty(pi);
        PropertyInfo p2 = new PropertyInfo();
        p2.setName("datetime");
        p2.setValue(datetime);
        p2.setType(String.class);
        request.addProperty(p2);

        SoapSerializationEnvelope envelope = new SoapSerializationEnvelope(
                SoapEnvelope.VER11);
        envelope.dotNet = true;
        envelope.setOutputSoapObject(request);
        HttpTransportSE httpTransport = new HttpTransportSE(soapaddress);
        try {
            httpTransport.call(SOAP_ACTION, envelope);
            response=envelope.toString();
            GMailSender sender = new GMailSender(user,pass);
            sender.sendMail(APP_NAME + " Error Report",error+"\n"+response,"reports@senseltelematics.com","vamsikrishna@sensel.in");
        } catch (Exception exception) {
            try {
                GMailSender sender = new GMailSender(user,pass);
                sender.sendMail(APP_NAME + " Error Recording failed",exception.toString()+"\n"+response,"reports@senseltelematics.com","vamsikrishna@sensel.in");
            } catch (Exception e) {
                Log.e("SendMail", e.getMessage(), e);
            }
        }
    }
}
