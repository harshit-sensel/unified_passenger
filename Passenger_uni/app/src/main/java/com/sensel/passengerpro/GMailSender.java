package com.sensel.passengerpro;

/**
 * Created by User on 07-07-2016.
 */
import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.security.Security;
import java.util.Properties;

import javax.activation.DataSource;
import javax.mail.Message;
import javax.mail.PasswordAuthentication;
import javax.mail.Session;
import javax.mail.Transport;
import javax.mail.internet.InternetAddress;
import javax.mail.internet.MimeMessage;

public class GMailSender extends javax.mail.Authenticator {
    private String mailhost = "smtp.gmail.com";
    private String user;
    private String password;
    private Session session;

    static {
        Security.addProvider(new JSSEProvider());
    }

    public GMailSender(String user, String password) {
        this.user = user;
        this.password = password;

        Properties props = new Properties();
        //For Gmail settings
      /*  props.setProperty("mail.transport.protocol", "smtp");
        props.setProperty("mail.host", mailhost);
        props.put("mail.smtp.auth", "true");
        props.put("mail.smtp.port", "465");//465
        props.put("mail.smtp.socketFactory.port", "465");
        props.put("mail.smtp.socketFactory.class",
                "javax.net.ssl.SSLSocketFactory");
        props.put("mail.smtp.socketFactory.fallback", "false");
        props.setProperty("mail.smtp.quitwait", "false");*/
        /* props.put("mail.smtp.socketFactory.class",
                "javax.net.ssl.SSLSocketFactory");
        props.put("mail.smtp.socketFactory.fallback", "false");
        props.setProperty("mail.smtp.quitwait", "false");*/
        //  session = Session.getInstance(props, this);

        //setting for amazon server
        props.setProperty("mail.transport.protocol", "smtps");
        props.setProperty("mail.host", "email-smtp.us-east-1.amazonaws.com");
        props.put("mail.smtp.auth", "true");

        props.put("mail.smtp.starttls.enable", "true");
        props.put("mail.smtp.starttls.required", "true");

        props.put("mail.smtp.ssl.enable", "true");
        props.put("mail.smtp.port", "25");//465
        props.put("mail.smtp.socketFactory.port", "25");

        session = Session.getDefaultInstance(props, this);
    }

    protected PasswordAuthentication getPasswordAuthentication() {
        return new PasswordAuthentication(user, password);
    }

    public synchronized void sendMail(String subject, String body, String sender, String recipients) throws Exception {

        Transport transport = session.getTransport();
        try{
           /* MimeMessage message = new MimeMessage(session);
            DataHandler handler = new DataHandler(new ByteArrayDataSource(body.getBytes(), "text/plain"));
            message.setSender(new InternetAddress(sender));
            message.setSubject(subject);
            message.setDataHandler(handler);*/

            MimeMessage msg = new MimeMessage(session);
            msg.setFrom(new InternetAddress(sender));
            msg.setSubject(subject);
            msg.setContent(body,"text/plain");

            if (recipients.indexOf(',') > 0)
                msg.setRecipients(Message.RecipientType.TO, InternetAddress.parse(recipients));
            else
                msg.setRecipient(Message.RecipientType.TO, new InternetAddress(recipients));

            // Connect to Amazon SES using the SMTP username and password you specified above.
            transport.connect("email-smtp.us-east-1.amazonaws.com", this.user, this.password);
            // Send the email.
            transport.sendMessage(msg, msg.getAllRecipients());
            // System.out.println("Email sent!");
        }
        catch (Exception ex) {
            ex.printStackTrace();
            // System.out.println("The email was not sent.");
            //  System.out.println("Error message: " + ex.getMessage());
        }
        finally
        {
            // Close and terminate the connection.
            transport.close();
        }

        //}
    }

    public class ByteArrayDataSource implements DataSource {
        private byte[] data;
        private String type;

        public ByteArrayDataSource(byte[] data, String type) {
            super();
            this.data = data;
            this.type = type;
        }

        public ByteArrayDataSource(byte[] data) {
            super();
            this.data = data;
        }

        public void setType(String type) {
            this.type = type;
        }

        public String getContentType() {
            if (type == null)
                return "application/octet-stream";
            else
                return type;
        }

        public InputStream getInputStream() throws IOException {
            return new ByteArrayInputStream(data);
        }

        public String getName() {
            return "ByteArrayDataSource";
        }

        public OutputStream getOutputStream() throws IOException {
            throw new IOException("Not Supported");
        }
    }
}